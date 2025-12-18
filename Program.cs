using QBFC17Lib;
using System;
using System.IO;
using QBXMLRPLib;
using static System.Collections.Specialized.BitVector32;
using QBXMLRP2Lib;

namespace AIW_EasyPack
{
    public class Program
    {
        static void Main(string[] args)
        {
            QBSessionManager session = new QBSessionManager();

            try
            {
                session.OpenConnection("", "Amount In word tool");
                session.BeginSession("", ENOpenMode.omDontCare);

                DateTime lastRun = LoadLastRunTime();

                IMsgSetRequest request = session.CreateMsgSetRequest("US", 13, 0);
                request.Attributes.OnError = ENRqOnError.roeContinue;

                IInvoiceQuery invoiceQuery = request.AppendInvoiceQueryRq();

                IMsgSetResponse response = session.DoRequests(request);

                ProcessInvoices(session,response,lastRun);

                SaveLastRunTime(DateTime.Now);

                session.EndSession();
                session.CloseConnection();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:" + ex.Message);
            }
            Console.ReadLine(); 
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
        static void UpdateInvoiceAmountInWords(string qbFilePath, string txnID, string amountInWords)
        {
            RequestProcessor2 rp = new RequestProcessor2();

            rp.OpenConnection("", "Amount In Words Tool");
            rp.BeginSession(qbFilePath, QBXMLRP2Lib.QBFileMode.qbFileOpenDoNotCare);

            string ticket = rp.BeginSession(qbFilePath, QBXMLRP2Lib.QBFileMode.qbFileOpenDoNotCare);
            string qbxml =
$@"<?xml version=""1.0""?>
<?qbxml version=""13.0""?>
<QBXML>
  <QBXMLMsgsRq onError=""stopOnError"">
    <DataExtAddRq>
      <DataExtAdd>
        <OwnerID>0</OwnerID>
        <DataExtName>AmountInWords</DataExtName>
        <TxnID>{txnID}</TxnID>
        <TxnType>Invoice</TxnType>
        <DataExtValue>{System.Security.SecurityElement.Escape(amountInWords)}</DataExtValue>
      </DataExtAdd>
    </DataExtAddRq>
  </QBXMLMsgsRq>
</QBXML>";

            rp.ProcessRequest(ticket,qbxml);

            rp.EndSession(ticket);
            rp.CloseConnection();
        }
        static void ProcessInvoices(QBSessionManager session,IMsgSetResponse response, DateTime lastRun)
        {
            if (response.ResponseList == null || response.ResponseList.Count == 0)
                return;

            IResponse resp = response.ResponseList.GetAt(0);
            if (resp.StatusCode != 0 || resp.Detail == null)
                return;

            IInvoiceRetList invoiceList = resp.Detail as IInvoiceRetList;
            if (invoiceList == null) return;

            for (int i = 0; i < invoiceList.Count; i++)
            {
                IInvoiceRet inv = invoiceList.GetAt(i);

                if (inv.TimeModified == null)
                    continue;

                DateTime modifiedTime = inv.TimeModified.GetValue();

                
                if (modifiedTime <= lastRun)
                    continue;

                decimal invoiceTotal = 0;

                if (inv.ORInvoiceLineRetList != null)
                {
                    for (int j = 0; j < inv.ORInvoiceLineRetList.Count; j++)
                    {
                        IORInvoiceLineRet orLine = inv.ORInvoiceLineRetList.GetAt(j);

                        if (orLine.InvoiceLineRet != null &&
                            orLine.InvoiceLineRet.Amount != null)
                        {
                            invoiceTotal += (decimal)orLine.InvoiceLineRet.Amount.GetValue();
                        }
                    }
                }
                string words = NumberToWords(invoiceTotal);
                UpdateInvoiceAmountInWords("", inv.TxnID.GetValue(), words);

                string refNo = inv.RefNumber?.GetValue() ?? "(no ref)";
                Console.WriteLine($"Invoice {refNo} → Total = {invoiceTotal}");
            }  
        }
        static string NumberToWords(decimal number)
        {
            if (number == 0)
                return "Zero Only";

            long integerPart = (long)Math.Floor(number);
            int fraction = (int)((number - integerPart) * 100);

            return $"{IntToWords(integerPart)} Rupees"
                 + (fraction > 0 ? $" and {IntToWords(fraction)} Cents" : "")
                 + " Only";
        }

        static string IntToWords(long number)
        {
            if (number == 0)
                return "";

            if (number < 0)
                return "Minus " + IntToWords(Math.Abs(number));

            string[] units = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
                       "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen",
                       "Eighteen", "Nineteen" };

            string[] tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                return units[number];

            if (number < 100)
                return tens[number / 10] + " " + units[number % 10];

            if (number < 1000)
                return units[number / 100] + " Hundred " + IntToWords(number % 100);

            if (number < 100000)
                return IntToWords(number / 1000) + " Thousand " + IntToWords(number % 1000);

            if (number < 10000000)
                return IntToWords(number / 100000) + " Lakh " + IntToWords(number % 100000);

            return IntToWords(number / 10000000) + " Crore " + IntToWords(number % 10000000);
        }
    }
}
