using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CorpusSearch.Service
{
    public class IMuseumNewspaperService
    {
        /// <summary>A reference to a specific clipping of a newspaper</summary>
        /// <remarks>Example: https://www.imuseum.im/Olive/APA/IsleofMan/get/image.ashx?kind=block&href=MNH%2F1833%2F10%2F25&id=Ar0080001&ext=.png</remarks>
        public class NewspaperClippingReference
        {
            /// <summary>The ID of the clipping in the newspaper database</summary>
            /// <remarks>This is typically </remarks>
            public string NewspaperClippingReferenceId { get; set; }

            public NewspaperReference Reference { get; set; }
            public string Href => Reference.Href;

            internal static NewspaperClippingReference FromOrThrow(string newspaper, string date, string id)
            {
                if (!NewspaperNameToId.ContainsValue(newspaper))
                {
                    throw new ArgumentException($"'{newspaper}' is not a valid newspaper reference. Example: 'MNH' refers to Mona's Herald");
                }

                DateTime d = DateTime.Parse(date);

                return new NewspaperClippingReference()
                {
                    NewspaperClippingReferenceId = id,
                    Reference = new NewspaperReference()
                    {
                        Date = d,
                        NewspaperIdentifier = newspaper
                    }
                };
            }
        }

        /// <summary>A component consists of one or more chunks</summary>
        /// <remarks>Typically what we're after whn viewing, this is one or more items of Manx text</remarks>
        public class NewspaperComponent
        {
            public string ComponentId { get; set; }

            public NewspaperReference Reference { get; set; }
            public string Href => Reference.Href;

            internal static NewspaperComponent FromOrThrow(string newspaper, string date, string componentId)
            {
                if (!NewspaperNameToId.ContainsValue(newspaper))
                {
                    throw new ArgumentException($"'{newspaper}' is not a valid newspaper reference. Example: 'MNH' refers to Mona's Herald");
                }

                DateTime d = DateTime.Parse(date);

                return new NewspaperComponent()
                {
                    ComponentId = componentId,
                    Reference = new NewspaperReference()
                    {
                        Date = d,
                        NewspaperIdentifier = newspaper
                    }
                };
            }
        }

        /// <summary>A reference to an issue of a newspaper, with an optional page</summary>
        /// <remarks>It is not easy to obtain a browsable source from this style of reference</remarks>
        public class NewspaperReference
        {
            public DateTime Date { get; set; }

            /// <summary>Internal MNH identifier for the newspaper. "CHU" for Camp Humor</summary>
            public string NewspaperIdentifier { get; set; }

            public int? Page { get; set; }
            public string Href => $"{NewspaperIdentifier}%2F{Date.Year}%2F{Date.Month:D2}%2F{Date.Day:D2}";
        }

        // Obtained from https://www.imuseum.im/Olive/APA/IsleofMan/get.res?id=page.Scripts&kind=script&uq=20210325071104&for=%7E%2Fdefault.aspx&mode=group
        public static Dictionary<string, string> NewspaperNameToId = new()
        {
            ["Calf of Man Bird Observatory Report"] = "COM",
            ["Camp Echo"] = "CEC",
            ["Camp Humor"] = "CHU",
            ["Camp Zeitung"] = "KCZ",
            ["Castletown Gazette"] = "CTG",
            ["Das Schleierlicht"] = "DSC",
            ["German Gymnastics Association"] = "MGN",
            ["Green Final"] = "GFL",
            ["Holiday News"] = "HNS",
            ["Isle of Man Daily Times"] = "IDT",
            ["Isle of Man Examiner"] = "IME",
            ["Isle of Man Times"] = "IMT",
            ["Isle of Man Weekly Advertising Circular"] = "WAC",
            ["Isle of Man Weekly Gazette"] = "IWG",
            ["Journal of The Manx Museum"] = "JMM",
            ["Lager Echo"] = "LAE",
            ["Lager Laterne"] = "DLA",
            ["Lager Zeitung"] = "LAZ",
            ["Lager-Ulk"] = "LAU",
            ["Manks Advertiser"] = "MNA",
            ["Manks Mercury"] = "MNM",
            ["Manx Cat"] = "TMC",
            ["Manx Free Press"] = "TFP",
            ["Manx Liberal"] = "MNB",
            ["Manx Museum and National Trust Report"] = "MMN",
            ["Manx Patriot"] = "MNP",
            ["Manx Rising Sun"] = "MRS",
            ["Manx Star"] = "MNS",
            ["Manx Sun"] = "TMS",
            ["Manxman"] = "TMN",
            ["Mona Daily Programme"] = "MDP",
            ["Mona's Herald"] = "MNH",
            ["Mona’s Herald"] = "MNH",
            ["Monas Herald"] = "MNH",
            ["Peel City Guardian"] = "PCG",
            ["Peel Sentinel"] = "PSL",
            ["Quousque Tandem"] = "QUT",
            ["Ramsey Chronicle"] = "RCE",
            ["Ramsey Courier"] = "RYC",
            ["Ramsey Weekly News"] = "RWN",
            ["Rising Sun"] = "TRS",
            ["TT Special"] = "TTS",
            ["Unter Uns"] = "UNU",
            ["Werden"] = "WER",
        };
    }
}
