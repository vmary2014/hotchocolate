using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Utilities;
using static HotChocolate.Execution.ExecutionResultKind;
using static HotChocolate.Execution.ThrowHelper;

namespace HotChocolate.Execution.Serialization;

/// <summary>
/// The default MultiPart formatter for <see cref="IExecutionResult"/>.
/// https://github.com/graphql/graphql-over-http/blob/master/rfcs/IncrementalDelivery.md
/// </summary>
public sealed partial class MultiPartResponseStreamFormatter : IExecutionResultFormatter
{
    private readonly IQueryResultFormatter _payloadFormatter;

    /// <summary>
    /// Creates a new instance of <see cref="MultiPartResponseStreamFormatter" />.
    /// </summary>
    /// <param name="indented">
    /// Defines whether the underlying <see cref="Utf8JsonWriter"/>
    /// should pretty print the JSON which includes:
    /// indenting nested JSON tokens, adding new lines, and adding
    /// white space between property names and values.
    /// By default, the JSON is written without any extra white space.
    /// </param>
    /// <param name="encoder">
    /// Gets or sets the encoder to use when escaping strings, or null to use the default encoder.
    /// </param>
    public MultiPartResponseStreamFormatter(
        bool indented = false,
        JavaScriptEncoder? encoder = null)
    {
        _payloadFormatter = new JsonQueryResultFormatter(indented, encoder);
    }

    /// <summary>
    /// Creates a new instance of <see cref="MultiPartResponseStreamFormatter" />.
    /// </summary>
    /// <param name="queryResultFormatter">
    /// The serializer that shall be used to serialize query results.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="queryResultFormatter"/> is <c>null</c>.
    /// </exception>
    public MultiPartResponseStreamFormatter(
        IQueryResultFormatter queryResultFormatter)
    {
        _payloadFormatter = queryResultFormatter ??
            throw new ArgumentNullException(nameof(queryResultFormatter));
    }

    /// <inheritdoc cref="IExecutionResultFormatter.FormatAsync"/>
    public ValueTask FormatAsync(
        IExecutionResult result,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (result is null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        return result.Kind switch
        {
            SingleResult =>
                WriteSingleResponseAsync(
                    (IQueryResult)result,
                    outputStream,
                    cancellationToken),
            DeferredResult or BatchResult or SubscriptionResult
                => WriteManyResponsesAsync(
                    (IResponseStream)result,
                    outputStream,
                    cancellationToken),
            _ => throw MultiPartFormatter_ResultNotSupported(
                nameof(MultiPartResponseStreamFormatter))
        };
    }

    /// <summary>
    /// Formats a response stream and writes the formatted result to
    /// the given <paramref name="outputStream"/>.
    /// </summary>
    /// <param name="responseStream">
    /// The response stream that shall be formatted.
    /// </param>
    /// <param name="outputStream">
    /// The stream to which the formatted <paramref name="responseStream"/> shall be written to.
    /// </param>
    /// <param name="cancellationToken">
    /// The cancellation token.
    /// </param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException"></exception>
    public ValueTask FormatAsync(
        IResponseStream responseStream,
        Stream outputStream,
        CancellationToken cancellationToken = default)
    {
        if (responseStream is null)
        {
            throw new ArgumentNullException(nameof(responseStream));
        }

        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream));
        }

        return WriteManyResponsesAsync(responseStream, outputStream, cancellationToken);
    }

    private async ValueTask WriteManyResponsesAsync(
        IResponseStream responseStream,
        Stream outputStream,
        CancellationToken ct = default)
    {
        // first we create the iterator.
        await using var enumerator =  responseStream.ReadResultsAsync().GetAsyncEnumerator(ct);

        // next we write a leading CRLF
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);

        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
        {
            try
            {
                // Before each part of the multi-part response, a boundary (---, CRLF)
                // is sent.
                await WriteNextAsync(outputStream, ct).ConfigureAwait(false);

                // Now we can write the header and body of the part.
                await WriteResultAsync(enumerator.Current, outputStream, ct).ConfigureAwait(false);

                // after each result we write a CRLF signaling the next or final part.
                await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);

                // we flush to make sure that the result is written to the network stream.
                await outputStream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                // The result objects use pooled memory so we need to ensure that they
                // return the memory by disposing them.
                await enumerator.Current.DisposeAsync().ConfigureAwait(false);
            }
        }

        // After the final payload, the terminating boundary of
        // ----- followed by CRLF is sent.
        await WriteEndAsync(outputStream, ct).ConfigureAwait(false);
        await outputStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask WriteSingleResponseAsync(
        IQueryResult queryResult,
        Stream outputStream,
        CancellationToken ct = default)
    {
        // first we write a leading CRLF
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);

        // Before each part of the multi-part response, a boundary (---, CRLF)
        // is sent.
        await WriteNextAsync(outputStream, ct).ConfigureAwait(false);

        try
        {
            // Now we can write the header and body of the part.
            await WriteResultAsync(queryResult, outputStream, ct).ConfigureAwait(false);

            // after each result we write a CRLF signaling the next or final part.
            await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
        }
        finally
        {
            // The result objects use pooled memory so we need to ensure that they
            // return the memory by disposing them.
            await queryResult.DisposeAsync().ConfigureAwait(false);
        }

        // After the final payload, the terminating boundary of
        // ----- followed by CRLF is sent.
        await WriteEndAsync(outputStream, ct).ConfigureAwait(false);
        await outputStream.FlushAsync(ct).ConfigureAwait(false);
    }

    private async ValueTask WriteResultAsync(
        IQueryResult result,
        Stream outputStream,
        CancellationToken ct)
    {
        using var writer = new ArrayWriter();
        _payloadFormatter.Format(result, writer);

        await WriteResultHeaderAsync(outputStream, ct).ConfigureAwait(false);

        // The payload is sent, followed by a CRLF.
        var buffer = writer.GetInternalBuffer();
        await outputStream.WriteAsync(buffer, 0, writer.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteResultHeaderAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        // Each part of the multipart response must contain a Content-Type header.
        // Similar to the GraphQL specification this specification does not require
        // a specific serialization format. For consistency and ease of notation,
        // examples of the response are given in JSON throughout the spec.
        await outputStream.WriteAsync(ContentType, 0, ContentType.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);

        // After all headers, an additional CRLF is sent.
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteNextAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        await outputStream.WriteAsync(Start, 0, Start.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }

    private static async ValueTask WriteEndAsync(
        Stream outputStream,
        CancellationToken ct)
    {
        await outputStream.WriteAsync(End, 0, End.Length, ct).ConfigureAwait(false);
        await outputStream.WriteAsync(CrLf, 0, CrLf.Length, ct).ConfigureAwait(false);
    }
}
