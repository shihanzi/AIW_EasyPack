using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIW_EasyPack.License
{
    public class LicenseHelper
    {
        public static string GetMachineId()
        {
            string cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
            string user = Environment.UserName;
            string machine = Environment.MachineName;

            return $"{cpu}|{machine}|{user}";
        }

        public static string GetMachineHash()
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(GetMachineId());
                return Convert.ToBase64String(sha.ComputeHash(bytes));
            }
        }

        public static bool ValidateLicense(string licenseKey)
        {
            string machineHash = GetMachineHash().Trim();

            File.AppendAllText(
                "aiw.log",
                $"VALIDATING MACHINE HASH: {machineHash}\r\n"
            );

            string expectedKey = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(machineHash + "|AIW-2025")
            );

            File.AppendAllText(
                "aiw.log",
                $"EXPECTED KEY: {expectedKey}\r\n"
            );

            return licenseKey.Trim() == expectedKey.Trim();
        }

        public static void SaveLicense(string key)
        {
            File.WriteAllText("license.dat", key);
        }

        public static string LoadLicense()
        {
            return File.Exists("license.dat")
                ? File.ReadAllText("license.dat")
                : null;
        }

    }
}
