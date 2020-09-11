using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChromeBookmarksCheck.Model;
using LiteDB;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ChromeBookmarksCheckUrl
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly List<Result> listResult = new List<Result>();

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            var sw = new Stopwatch();
            sw.Start();
			
            var filePath = ConfigurationManager.AppSettings["BookmarksPath"];

            using (var streamReader = new StreamReader(filePath))
            {
                using (var reader = new JsonTextReader(streamReader))
                {
                    using (var db = new LiteDatabase(@"CheckOK.db"))
                    {
                        var col = db.GetCollection<CheckResult>("CheckResults");
                        col.EnsureIndex(x => x.Name, true);

                        var rootObject = new Newtonsoft.Json.JsonSerializer().Deserialize<Rootobject>(reader);

                        if (rootObject != null)
                        {
                            await ProcessChildren(rootObject.roots.bookmark_bar.children, string.Empty, col);
                        }
                    }
                }
            }

            ExportExcel();

            sw.Stop();
            Console.WriteLine($"總共花費{sw.Elapsed.Hours}:{sw.Elapsed.Minutes}:{sw.Elapsed.Seconds}");

            Console.WriteLine("Enter any key!");
            Console.ReadKey();
        }

        private static async Task ProcessChildren(Child[] childArr, string folderPath, ILiteCollection<CheckResult> col)
        {
            //排序讓資料夾先處理
            var childrenSort = from e in childArr
                               orderby e.type, e.name
                               select e;

            foreach (var item in childrenSort)
            {
                if (item.type == "folder")
                {
                    folderPath += "/" + item.name;
                    //如果有資料夾，就先處理
                    await ProcessChildren(item.children, folderPath, col);
                    folderPath = folderPath.Replace("/" + item.name, string.Empty);
                }
                else
                {
                    try
                    {
                        //找之前是否有檢查成功的記錄，如果有就不用再檢查了
                        var findtext = col.Find(Query.EQ(nameof(CheckResult.Name), item.name));
                        if (findtext.Any()) continue;

                        Console.WriteLine($"資料夾：{folderPath}");
                        Console.WriteLine($"名稱：{item.name}");
                        Console.WriteLine($"網址：{item.url}");
                        var response = await client.GetAsync(item.url);
                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine($"HTTP 狀態碼：{(int)response.StatusCode}");

                            listResult.Add(new Result { FolderPath = folderPath, Name = item.name, Url = item.url, ErrMsg = $"HTTP 狀態碼：{(int)response.StatusCode}" });
                        }
                        else
                        {
                            var checkResult = new CheckResult
                            {
                                Name = item.name,
                                Link = item.url
                            };
                            col.Insert(checkResult);
                        }
                    }
                    catch (HttpRequestException e)
                    {
                        Console.WriteLine($"錯誤訊息：{e.Message}");
                        var message = e.Message;
                        if (e.InnerException != null)
                        {
                            Console.WriteLine($"內部錯誤訊息：{e.InnerException.Message}");
                            message += e.InnerException.Message;
                        }

                        listResult.Add(new Result { FolderPath = folderPath, Name = item.name, Url = item.url, ErrMsg = message });
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"錯誤訊息：{e.Message}");
                        var message = e.Message;
                        if (e.InnerException != null)
                        {
                            Console.WriteLine($"內部錯誤訊息：{e.InnerException.Message}");
                            message += e.InnerException.Message;
                        }

                        listResult.Add(new Result { FolderPath = folderPath, Name = item.name, Url = item.url, ErrMsg = message });
                    }

                    Console.WriteLine(string.Empty);
                }
            }
        }

        private static void ExportExcel()
        {
            var f = new FileInfo(ConfigurationManager.AppSettings["ExportExcelPath"]);
            if (f.Exists)
                f.Delete();

            using (var excelFile = new ExcelPackage(f))
            {
                var ws = excelFile.Workbook.Worksheets.Add("Sheet1");
                ws.Cells["A1"].LoadFromCollection(listResult, true);

                //Format the header for column，依 Properties 的欄位決定 
                using (var rng = ws.Cells[1, 1, 1, typeof(Result).GetProperties().Length])
                {
                    rng.Style.Font.Bold = true;
                    rng.Style.Fill.PatternType = ExcelFillStyle.Solid; //Set Pattern for the background to Solid
                    rng.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(79, 129, 189)); //Set color to dark blue
                    rng.Style.Font.Color.SetColor(Color.White);
                }

                //欄寬自動設定
                ws.Cells.AutoFitColumns();

                excelFile.Save();
            }
        }
    }
}
