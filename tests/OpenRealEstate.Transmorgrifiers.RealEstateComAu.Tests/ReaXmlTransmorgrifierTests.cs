using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenRealEstate.Core.Filters;
using OpenRealEstate.Core.Rental;
using OpenRealEstate.Core.Residential;
using Shouldly;
using Xunit;

namespace OpenRealEstate.Transmorgrifiers.RealEstateComAu.Tests
{
    public class ReaXmlTransmorgrifierTests : SetupTests
    {
        [Fact]
        public void GivenTheFileREAAllTypes_Parse_ReturnsAListOfListings()
        {
            // Arrange.
            var reaXml = File.ReadAllText("Sample Data/REA-AllTypes.xml");
            var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

            // Act.
            var result = reaXmlTransmorgrifier.Parse(reaXml);

            // Assert.
            result.Listings.Count.ShouldBe(6);
            result.UnhandledData.Count.ShouldBe(0);
            result.Errors.Count.ShouldBe(0);

            var listings = result.Listings.Select(x => x.Listing).ToList();

            var residentialCurrentListing = listings
                .AsQueryable()
                .WithId("Residential-Current-ABCD1234")
                .OfType<ResidentialListing>()
                .SingleOrDefault();
            residentialCurrentListing.ShouldNotBeNull();

            var residentialSoldListing = listings
                .AsQueryable()
                .WithId("Residential-Sold-ABCD1234")
                .OfType<ResidentialListing>()
                .SingleOrDefault();
            residentialSoldListing.ShouldNotBeNull();

            var residentialWithdrawnListing = listings
                .AsQueryable()
                .WithId("Residential-Withdrawn-ABCD1234")
                .OfType<ResidentialListing>()
                .SingleOrDefault();
            residentialWithdrawnListing.ShouldNotBeNull();

            var rentalCurrentListing = listings
                .AsQueryable()
                .WithId("Rental-Current-ABCD1234")
                .OfType<RentalListing>()
                .SingleOrDefault();
            rentalCurrentListing.ShouldNotBeNull();

            var rentalLeasedListing = listings
                .AsQueryable()
                .WithId("Rental-Leased-ABCD1234")
                .OfType<RentalListing>()
                .SingleOrDefault();
            rentalLeasedListing.ShouldNotBeNull();

            var rentalListing = listings
                .AsQueryable()
                .WithId("Rental-Withdrawn-ABCD1234")
                .OfType<RentalListing>()
                .SingleOrDefault();
            rentalListing.ShouldNotBeNull();
        }

        [Fact]
        public void GivenTheFileREABadContent_Parse_ThrowsAnException()
        {
            // Arrange.
            var reaXml = File.ReadAllText("Sample Data/REA-BadContent.xml");
            var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

            // Act.
            var result = reaXmlTransmorgrifier.Parse(reaXml);

            // Assert.
            result.Listings.Count.ShouldBe(0);
            result.UnhandledData.Count.ShouldBe(0);
            result.Errors.Count.ShouldBe(1);

            result.Errors.First()
                    .ExceptionMessage.ShouldBe(
                        "Unable to parse the xml data provided. Currently, only a <propertyList/> or listing segments <residential/> / <rental/> / <land/> / <rural/>. Root node found: 'badContent'.");
            result.Errors.First()
                    .InvalidData.ShouldBe(
                        "Failed to parse the provided xml data because it contains some invalid data. Pro Tip: This is usually because a character is not encoded. Like an ampersand.");
        }

        [Fact]
        public void GivenTheFileREAInvalidCharacterAndBadCharacterCleaning_Parse_ThrowsAnException()
        {
            // Arrange.
            var reaXml = File.ReadAllText("Sample Data/REA-InvalidCharacter.xml");
            var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

            // Act.
            var result = reaXmlTransmorgrifier.Parse(reaXml, areBadCharactersRemoved: true);

            // Assert.
            result.Listings.Count.ShouldBe(1);

            var residentialCurrentListing = result.Listings
                                                    .Select(x => x.Listing)
                                                    .AsQueryable()
                                                    .WithId("Residential-Current-ABCD1234")
                                                    .OfType<ResidentialListing>()
                                                    .SingleOrDefault();
            residentialCurrentListing.ShouldNotBeNull();

            result.Errors.Count.ShouldBe(0);
            result.UnhandledData.Count.ShouldBe(0);
        }

        [Theory]
        [InlineData("<b>Best listing ever!</b>", "<b>This is a great listing!</b>", "Best listing ever!", "This is a great listing!")] // Simple bold tags, removed.
        [InlineData("The price of apples are < the price of <b>oranges</b>", "Hi Hi", "The price of apples are < the price of oranges", "Hi Hi")] // Random < symbol in title remains. No html in description.
        [InlineData("How are things < today? Are they ok? > > > yo yo yo", "hi", "How are things < today? Are they ok? > > > yo yo yo", "hi")]
        public void GivenTheFileREAHtmlInTitleAndDesc_Parse_ReturnsAListOfListingsWithHTMLRemovedFromTitleAndDescription(string title,
                                                                                                                         string description,
                                                                                                                         string titleOut,
                                                                                                                         string descriptionOut)
        {
            static string ToXmlEncodedString(string text)
            {
                return new XElement("t", text).LastNode.ToString();
            }

            // Arrange.
            var xmlEncodedTitle = ToXmlEncodedString(title);
            var xmlEncodedDescription = ToXmlEncodedString(description);
            var reaXml = File.ReadAllText("Sample Data/Residential/REA-Residential-Current-WithHeadlineAndDescriptionPlaceholders.xml");            
            var updatedXml = reaXml.Replace("REPLACE-THIS-TITLE", xmlEncodedTitle)
                                   .Replace("REPLACE-THIS-DESCRIPTION", xmlEncodedDescription);
            var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

            // Act.
            var result = reaXmlTransmorgrifier.Parse(updatedXml);

            // Assert.
            result.Listings.Count.ShouldBe(1);

            var residentialCurrentListing = result.Listings
                                                  .Select(x => x.Listing)
                                                  .AsQueryable()
                                                  .WithId("Residential-Current-ABCD1234")
                                                  .OfType<ResidentialListing>()
                                                  .SingleOrDefault();
            residentialCurrentListing.ShouldNotBeNull();
            residentialCurrentListing.Title.ShouldBe(titleOut);
            residentialCurrentListing.Description.ShouldBe(descriptionOut);
            result.Errors.Count.ShouldBe(0);
            result.UnhandledData.Count.ShouldBe(0);
        }

        [Fact]
        public void GivenTheFileREAInvalidCharacterAndNoBadCharacterCleaning_Parse_ThrowsAnException()
        {
            // Arrange.
            var reaXml = File.ReadAllText("Sample Data/REA-InvalidCharacter.xml");
            var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

            // Act.
            var result = reaXmlTransmorgrifier.Parse(reaXml);

            // Assert.
            result.Listings.Count.ShouldBe(0);
            result.UnhandledData.Count.ShouldBe(0);
            result.Errors.Count.ShouldBe(1);
            result.Errors.First()
                    .ExceptionMessage.ShouldBe(
                        "The REA Xml data provided contains some invalid characters. Line: 0, Position: 1661. Error: '\x16', hexadecimal value 0x16, is an invalid character. Suggested Solution: Either set the 'areBadCharactersRemoved' parameter to 'true' so invalid characters are removed automatically OR manually remove the errors from the file OR manually handle the error (eg. notify the people who sent you this data, that it contains bad data and they should clean it up.)");
            result.Errors.First().InvalidData.ShouldBe("The entire data source.");
        }

        [Fact]
        public void GivenTheFileREAMixedContent_Parse_ReturnsAParsedResultWithListingsAndUnhandedData()
            {
                // Arrange.
                var reaXml = File.ReadAllText("Sample Data/REA-MixedContent.xml");
                var reaXmlTransmorgrifier = new ReaXmlTransmorgrifier();

                // Act.
                var result = reaXmlTransmorgrifier.Parse(reaXml);

                // Assert.
                result.Listings.Count.ShouldBe(2);
                result.UnhandledData.Count.ShouldBe(3);
                result.UnhandledData[0].StartsWith("<pewPew1").ShouldBe(true);
                result.Errors.Count.ShouldBe(0);
            }
    }
}
