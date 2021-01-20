using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.StarWars;
using Microsoft.Extensions.DependencyInjection;
using Snapshooter.Xunit;
using StrawberryShake.CodeGeneration.Analyzers.Models;
using StrawberryShake.CodeGeneration.CSharp.Builders;
using Xunit;
using Xunit.Sdk;

namespace StrawberryShake.CodeGeneration.CSharp
{
    public class CSharpGeneratorExecutorTests
    {
        [Fact]
        public async Task Interface_With_Default_Names()
        {
            // arrange
            ClientModel clientModel =
                await TestHelper.CreateClientModelAsync(
                    @"query GetHero {
                        hero(episode: NEW_HOPE) {
                            name
                            appearsIn
                        }
                    }",
                    "extend schema @key(fields: \"id\")");

            // act
            var documents = new StringBuilder();
            var generator = new CSharpGeneratorExecutor();

            // assert
            AssertResult(clientModel, generator, documents);
        }

        [Fact]
        public async Task Interface_With_Fragment_Definition_Two_Models()
        {
            // arrange
            ClientModel clientModel =
                await TestHelper.CreateClientModelAsync(
                    @"query GetHero {
                        hero(episode: NEW_HOPE) {
                            ... Hero
                        }
                    }

                    fragment Hero on Character {
                        name
                        ... Human
                        ... Droid
                    }

                    fragment Human on Human {
                        homePlanet
                    }

                    fragment Droid on Droid {
                        primaryFunction
                    }",
                    "extend schema @key(fields: \"id\")");

            // act
            var documents = new StringBuilder();
            var generator = new CSharpGeneratorExecutor();

            // assert
            AssertResult(clientModel, generator, documents);
        }

        private static void AssertResult(
            ClientModel clientModel,
            CSharpGeneratorExecutor generator,
            StringBuilder documents)
        {
            foreach (CSharpDocument document in generator.Generate(clientModel, "Foo"))
            {
                documents.AppendLine("// " + document.Name);
                documents.AppendLine();
                documents.AppendLine(document.Source);
                documents.AppendLine();
            }

            documents.ToString().MatchSnapshot();
        }
    }
}