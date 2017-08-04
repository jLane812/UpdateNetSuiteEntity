using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UpdateNetSuiteMatrix.Model;

namespace UpdateNetSuiteMatrix
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var db = new SegmentAndPricingMatrixDifferencesDataContext())
            {
                var itemIds = db.vwSegmentAndPricingMatrixDifferences.Select(d => d.NSItemId).Distinct().ToList();

                var items = db.vwSegmentAndPricingMatrixDifferences.ToList(); 

                var continuedItems = items.Where(id => id.DISCONTINUED.Trim().ToUpper() == "F" && itemIds.Contains(Convert.ToInt32(id.NSItemId))).Select(id => Convert.ToInt32(id.NSItemId)).Distinct().ToList();
                var discontinuedItems = items.Where(id => id.DISCONTINUED.Trim().ToUpper() == "T" && itemIds.Contains(Convert.ToInt32(id.NSItemId))).Select(id => Convert.ToInt32(id.NSItemId)).Distinct().ToList();

                var matrixDifferences = db.vwSegmentAndPricingMatrixDifferences.ToList();

                PrepAndUpdateItems(continuedItems, matrixDifferences);
                PrepAndUpdateItems(discontinuedItems, matrixDifferences);
            }
        }

        static void PrepAndUpdateItems(List<int> itemIds, List<vwSegmentAndPricingMatrixDifference> differences)
        {
            var options = new ParallelOptions() { MaxDegreeOfParallelism = 15 };

            Parallel.ForEach(itemIds, options, id =>
            {
                var brand = differences.Select(br => br.SMBrand);
                var manufacturer = differences.Select(ma => ma.SMManufacturerOfItem);
                var pricingGroup = differences.Select(pgr => pgr.SMPricingGroup);
                var rebateGroup = differences.Select(rgr => rgr.SMRebateGroup);
                var vendors = differences.Where(d => Convert.ToInt32(d.NSItemId).Equals(id)).ToList();
                var preferredVendor = vendors.Select(pf => pf.NewPreferredVendorID).FirstOrDefault();
                UpdateNetSuite(id.ToString(), brand.ToString(), manufacturer.ToString(), pricingGroup.ToString(), rebateGroup.ToString(), preferredVendor.ToString(), vendors);
            });
        }

        static void UpdateNetSuite(string itemId, string brand, string manufacturer, string pricingGroup, string rebateGroup, string preferredVendor, List<vwSegmentAndPricingMatrixDifference> vendors)
        {
            var uri = "https://forms.netsuite.com/specificForms";
            var pars = itemId + "," + brand + "," + manufacturer + "," + pricingGroup + "," + rebateGroup + "," + preferredVendor + ",";

            foreach (var vendor in vendors)
            {
                pars += vendor.VENDOR_ID.ToString() + "," + vendor.NewCost.ToString() + ",";
            }

            try
            {
                using (MyWebClient client = new MyWebClient())
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    string HtmlResult = client.UploadString(uri, pars);
                    Console.WriteLine(HtmlResult);
                    Console.WriteLine();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error" + e);
            }
        }

        public class MyWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri uri)
            {
                WebRequest w = base.GetWebRequest(uri);
                w.Timeout = 20 * 60 * 1000;
                return w;
            }
        }
    }
}
