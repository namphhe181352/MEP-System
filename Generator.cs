using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

class Program
{
    static void Main()
    {
        string path = @""d:\�? �n\MEP-System\MEP_Management_System\Controllers"";
        var files = Directory.GetFiles(path, ""*Controller.cs"", SearchOption.AllDirectories);
        
        using (StreamWriter sw = new StreamWriter(@""d:\�? �n\MEP-System\Full_MethodList_Detailed.csv""))
        {
            sw.WriteLine(""Module Name,Controller,Method Name,HTTP Method,Route,Description,Pre-Condition"");
            
            foreach (var file in files)
            {
                string content = File.ReadAllText(file);
                string controllerName = Path.GetFileNameWithoutExtension(file);
                string moduleName = new DirectoryInfo(Path.GetDirectoryName(file)).Name.Replace(""Controller"", """");
                
                var methodRegex = new Regex(@""\[Http(Get|Post|Put|Delete|Patch)(.*?)\].*?public\s+async\s+Task<IActionResult>\s+(\w+)"", RegexOptions.Singleline);
                var matches = methodRegex.Matches(content);
                
                foreach (Match match in matches)
                {
                    string httpMethod = match.Groups[1].Value;
                    string route = match.Groups[2].Value.Replace(""(\"""", """").Replace(""\"")"", """");
                    string methodName = match.Groups[3].Value;
                    
                    sw.WriteLine($""{moduleName},{controllerName},{methodName},Http{httpMethod},{route},\""Execute {methodName} operation\"",\""User is authenticated\"""");
                }
            }
        }
    }

