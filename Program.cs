using QBFC17Lib;
using System;
using System.IO;
using QBXMLRPLib;
using static System.Collections.Specialized.BitVector32;
using QBXMLRP2Lib;
using System.Xml;

namespace AIW_EasyPack
{
    public class Program
    {
        static void Main(string[] args)
        {
            RequestProcessor2 rp = new RequestProcessor2();

            rp.OpenConnection("", "Amount In Words Tool");
            string ticket = rp.BeginSession("", QBXMLRP2Lib.QBFileMode.qbFileOpenDoNotCare);

            try
            {
                DateTime lastRun = LoadLastRunTime();

                //  READ invoices
                string invoiceQueryXml = GetInvoiceQueryXml();
                string responseXml = rp.ProcessRequest(ticket, invoiceQueryXml);

                //  PROCESS invoices
                ProcessInvoicesFromXml(rp, ticket, responseXml, lastRun);

                SaveLastRunTime(DateTime.Now);
            }
            catch (Exception ex)
            {
                LogError(ex);
                Console.WriteLine("An error occurred. Please check the log.");
            }
            finally
            {
                try
                {
                    if (ticket != null)
                        rp.EndSession(ticket);
                }
                catch { }

                try
                {
                    rp?.CloseConnection();
                }
                catch { }
            }

            void LogError(Exception ex)
            {
                File.AppendAllText(
                    "errors.log",
                    $"{DateTime.Now:o} | {ex}\r\n"
                );
            }

            string GetInvoiceQueryXml()
            {
                return @"<?xml version=""1.0""?>
<?qbxml version=""13.0""?>
<QBXML>
  <QBXMLMsgsRq onError=""stopOnError"">
    <InvoiceQueryRq>
      <IncludeLineItems>true</IncludeLineItems>
    </InvoiceQueryRq>
  </QBXMLMsgsRq>
</QBXML>";
            }
        }
        static void ProcessInvoicesFromXml(
            RequestProcessor2 rp,
            string ticket,
            string responseXml,
            DateTime lastRun)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(responseXml);

            XmlNodeList invoices = doc.SelectNodes("//InvoiceRet");
            if (invoices == null) return;

            foreach (XmlNode invoice in invoices)
            {
                DateTime modifiedTime;
                var modifiedNode = invoice.SelectSingleNode("TimeModified");
                if (modifiedNode == null) continue;

                modifiedTime = DateTime.Parse(modifiedNode.InnerText);
                if (modifiedTime <= lastRun) continue;

                string txnID = invoice.SelectSingleNode("TxnID")?.InnerText;
                if (string.IsNullOrEmpty(txnID)) continue;

                string editSeq = invoice.SelectSingleNode("EditSequence")?.InnerText;
                if (string.IsNullOrEmpty(editSeq)) continue;


                decimal total = 0;
                XmlNodeList lines = invoice.SelectNodes("InvoiceLineRet");

                foreach (XmlNode line in lines)
                {
                    var amtNode = line.SelectSingleNode("Amount");
                    if (amtNode != null)
                        total += decimal.Parse(amtNode.InnerText,System.Globalization.CultureInfo.InvariantCulture);
                }

                string words = NumberToWords(total);
                SendAmountInWords(rp, ticket, txnID, editSeq, words);

                string refNo = invoice.SelectSingleNode("RefNumber")?.InnerText ?? "(no ref)";
                Console.WriteLine($"Invoice {refNo} → {words}");
            }
        }

        static void SendAmountInWords(
    QBXMLRP2Lib.RequestProcessor2 rp,
    string ticket,
    string txnID,
    string editSequence,
    string amountInWords)
        {
            string[] lines = WrapForShipTo(amountInWords);

            string qbxml =
        $@"<?xml version=""1.0""?>
<?qbxml version=""13.0""?>
<QBXML>
  <QBXMLMsgsRq onError=""stopOnError"">
    <InvoiceModRq>
      <InvoiceMod>
        <TxnID>{txnID}</TxnID>
        <EditSequence>{editSequence}</EditSequence>
        <ShipAddress>
          {(string.IsNullOrEmpty(lines[0]) ? "" : $"<Addr1>{MakeQBXmlSafe(lines[0])}</Addr1>")}
          {(string.IsNullOrEmpty(lines[1]) ? "" : $"<Addr2>{MakeQBXmlSafe(lines[1])}</Addr2>")}
          {(string.IsNullOrEmpty(lines[2]) ? "" : $"<Addr3>{MakeQBXmlSafe(lines[2])}</Addr3>")}
          {(string.IsNullOrEmpty(lines[3]) ? "" : $"<Addr4>{MakeQBXmlSafe(lines[3])}</Addr4>")}
        </ShipAddress>
      </InvoiceMod>
    </InvoiceModRq>
  </QBXMLMsgsRq>
</QBXML>";

            rp.ProcessRequest(ticket, qbxml);
        }


        static string MakeQBXmlSafe(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            return input
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;")
                .Replace("\r", "")
                .Replace("\n", "")
                .Trim();
        }


        static string NumberToWords(decimal number)
        {
            if (number == 0)
                return "Zero";

            long integerPart = (long)Math.Floor(number);
            int fraction = (int)((number - integerPart) * 100);

            return $"{IntToWords(integerPart)}" //  $"{IntToWords(integerPart)} "Can put rupees here if needed" "
                 + (fraction > 0 ? $" and {IntToWords(fraction)} Cents" : "");
        }

        static string IntToWords(long number)
        {
            string[] units = {
                "", "One", "Two", "Three", "Four", "Five", "Six",
                "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve",
                "Thirteen", "Fourteen", "Fifteen", "Sixteen",
                "Seventeen", "Eighteen", "Nineteen"
            };

            string[] tens = {
                "", "", "Twenty", "Thirty", "Forty", "Fifty",
                "Sixty", "Seventy", "Eighty", "Ninety"
            };

            if (number == 0)
                return "";

            if (number < 0)
                return "Minus " + IntToWords(Math.Abs(number));

            if (number < 20)
                return units[number];

            if (number < 100)
                return tens[number / 10] +
                       (number % 10 > 0 ? " " + units[number % 10] : "");

            if (number < 1000)
                return units[number / 100] + " Hundred" +
                       (number % 100 > 0 ? " " + IntToWords(number % 100) : "");

            if (number < 1_000_000)
                return IntToWords(number / 1000) + " Thousand" +
                       (number % 1000 > 0 ? " " + IntToWords(number % 1000) : "");

            if (number < 1_000_000_000)
                return IntToWords(number / 1_000_000) + " Million" +
                       (number % 1_000_000 > 0 ? " " + IntToWords(number % 1_000_000) : "");

            return IntToWords(number / 1_000_000_000) + " Billion" +
                   (number % 1_000_000_000 > 0 ? " " + IntToWords(number % 1_000_000_000) : "");
        }

        static string[] WrapForShipTo(string text, int maxLineLength = 40, int maxLines = 4)
        {
            var words = text.Split(' ');
            var lines = new string[maxLines];

            int lineIndex = 0;
            lines[lineIndex] = "";

            foreach (var word in words)
            {
                if ((lines[lineIndex] + word).Length + 1 <= maxLineLength)
                {
                    lines[lineIndex] += (lines[lineIndex].Length == 0 ? "" : " ") + word;
                }
                else
                {
                    lineIndex++;
                    if (lineIndex >= maxLines)
                        break;

                    lines[lineIndex] = word;
                }
            }

            return lines;
        }



        static DateTime LoadLastRunTime()
        {
            if (!File.Exists("last_run.txt"))
                return DateTime.MinValue;

            return DateTime.Parse(File.ReadAllText("last_run.txt"));
        }

        static void SaveLastRunTime(DateTime dt)
        {
            File.WriteAllText("last_run.txt", dt.ToString("o"));
        }
    }
}
