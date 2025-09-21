//
// Copyright (c) 2001-2025 by Apryse Software Inc. All Rights Reserved.
//

using pdftron;
using pdftron.Common;
using pdftron.PDF;
using pdftron.SDF;
using System;
using System.Collections;
using System.Collections.Generic;


namespace TextSearchAndRedactCS
{
   // This sample illustrates various text search capabilities of PDFNet.

   class Class1
   {
      private static pdftron.PDFNetLoader pdfNetLoader = pdftron.PDFNetLoader.Instance();
      static Class1() { }

      static void Main(string[] args)
      {
         PDFNet.Initialize(PDFTronLicense.Key);

         // Relative path to the folder containing test files.
         string input_path = "../../";

         // Sample code showing how to use high-level text extraction APIs.

         try
         {
            PDFNet.Initialize();

            List<SearchItem> searchItems = new List<SearchItem>();

            // Specify plain text
            searchItems.Add(new SearchItem(SearchItemType.Text, "Robin Hood")); 

            // Specify regular expressions
            searchItems.Add(new SearchItem(SearchItemType.RegEx, "[a-zA-Z0-9._%+-]+@\\s?[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}")); // email address
            searchItems.Add(new SearchItem(SearchItemType.RegEx, @"\b(?:\+?1[-.\s]?)*\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b")); // US phone numbers
            searchItems.Add(new SearchItem(SearchItemType.RegEx, @"(\(?\d{4,5}\)?\s\d{3,4}\s\d{4})")); // UK phone numbers
            searchItems.Add(new SearchItem(SearchItemType.RegEx, @"\d{5}\s\d{6}")); // UK phone numbers
            searchItems.Add(new SearchItem(SearchItemType.RegEx, @"ext\s?(\d{5})")); // UK extensions
            searchItems.Add(new SearchItem(SearchItemType.RegEx, @"\b(?:https?://|www\.)[^\s<>]+(?:\.[^\s<>]+)+\b"));  // Web URLs

            string output_path = "../../";

            // Default appearance
            SearchAndRedact(input_path + "newsletter.pdf", output_path + "newsletter_redacted.pdf", searchItems);
            
            // Customize appearance
            Redactor.Appearance appearance = new Redactor.Appearance();
            appearance.RedactionOverlay = true;
            appearance.TextColor = System.Drawing.Color.Red;
            appearance.HorizTextAlignment = 0;
            appearance.VertTextAlignment = 0;
            appearance.PositiveOverlayColor = System.Drawing.Color.Black;
            appearance.RedactedContentColor = System.Drawing.Color.Black;
            SearchAndRedact(input_path + "newsletter.pdf", output_path + "newsletter_redacted_foia.pdf", searchItems, "FOIA", appearance);            
         }
         catch (PDFNetException e)
         {
            Console.WriteLine(e.Message);
         }
         PDFNet.Terminate();
      }
           

      // Overload 1: Source, Target, Phrases to find
      public static void SearchAndRedact(string sourcePath, string targetPath, IList<SearchItem> searchItems)
      {
         SearchAndRedact(sourcePath, targetPath, searchItems, string.Empty);
      }

      // Overload 2: Source, Target, Phrases to find, Overlay text 
      public static void SearchAndRedact(string sourcePath, string targetPath, IList<SearchItem> searchItems, string overlayText)
      {
         // Create default options for overlay text
         Redactor.Appearance appearance = new Redactor.Appearance();
         appearance.RedactionOverlay = true;
         appearance.TextColor = System.Drawing.Color.Red;
         appearance.HorizTextAlignment = 0;
         appearance.VertTextAlignment = 0;
         appearance.PositiveOverlayColor = System.Drawing.Color.Black;
         appearance.RedactedContentColor = System.Drawing.Color.Black;

         SearchAndRedact(sourcePath, targetPath, searchItems, overlayText, appearance);
      }

      // Overload 3: Source, Target, Phrases to find, Overlay Text, Customize Appearance
      public static void SearchAndRedact(string sourcePath, string targetPath, IList<SearchItem> searchItems, string overlayText, Redactor.Appearance appearance)
	   {
         var doc = new PDFDoc(sourcePath);
         doc.InitSecurityHandler();

         int MAX_RESULTS = 1000;
         int iteration = 0;
         Int32 page_num = 0;
         String result_str = "", ambient_string = "";
         Highlights hlts = new Highlights();

         var redactions = new ArrayList();

         // Step 1: Locate positions for each phrase using TextSearch
         foreach (SearchItem searchItem in searchItems)
         {            
            var txtSearch = new TextSearch();
            Int32 mode = (Int32)(TextSearch.SearchMode.e_whole_word | TextSearch.SearchMode.e_highlight);

            if (searchItem.ItemType == SearchItemType.RegEx)
            {
               mode |= (Int32)TextSearch.SearchMode.e_reg_expression;
               txtSearch.SetPattern(searchItem.Value);
            }

            txtSearch.Begin(doc, searchItem.Value, mode, -1, -1);
            TextSearch.ResultCode code = TextSearch.ResultCode.e_done;

            do
            {
               code = txtSearch.Run(ref page_num, ref result_str, ref ambient_string, hlts);

               if (code == TextSearch.ResultCode.e_found)
               {
                  hlts.Begin(doc);
                  while (hlts.HasNext())
                  {
                     double[] quads = hlts.GetCurrentQuads();
                     int quad_count = quads.Length / 8;
                     for (int i = 0; i < quad_count; ++i)
                     {
                        //assume each quad is an axis-aligned rectangle
                        int offset = 8 * i;
                        double x1 = Math.Min(Math.Min(Math.Min(quads[offset + 0], quads[offset + 2]), quads[offset + 4]), quads[offset + 6]);
                        double x2 = Math.Max(Math.Max(Math.Max(quads[offset + 0], quads[offset + 2]), quads[offset + 4]), quads[offset + 6]);
                        double y1 = Math.Min(Math.Min(Math.Min(quads[offset + 1], quads[offset + 3]), quads[offset + 5]), quads[offset + 7]);
                        double y2 = Math.Max(Math.Max(Math.Max(quads[offset + 1], quads[offset + 3]), quads[offset + 5]), quads[offset + 7]);

                        redactions.Add(new Redactor.Redaction(page_num, new Rect(x1, y1, x2, y2), false, overlayText));
                     }
                     hlts.Next();
                  }
               }
            } while ( (code != TextSearch.ResultCode.e_done) || (iteration++ < MAX_RESULTS) );
         }
                  
         // Step 2: Apply redactions
         Redactor.Redact(doc, redactions, appearance);
         doc.Save(targetPath, SDFDoc.SaveOptions.e_linearized);
      }
	}


   public class SearchItem
   {
      public SearchItemType ItemType { get; set; }
      public string Value { get; set; }

      public SearchItem(SearchItemType itemType, string value)
      {
         this.ItemType = itemType;
         this.Value = value;
      }
   }

   public enum SearchItemType
   {
      Text,
      RegEx
   }
}
