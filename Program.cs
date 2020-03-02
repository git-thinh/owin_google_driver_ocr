using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Microsoft.Owin;
using Microsoft.Owin.FileSystems;
using Microsoft.Owin.Hosting;
using Microsoft.Owin.StaticFiles;
using Owin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Topshelf;
using DataFile = Google.Apis.Drive.v2.Data.File;

namespace owin_google_driver_ocr
{  
    public class SimpleMiddleware : OwinMiddleware
    {
        public SimpleMiddleware(OwinMiddleware next) : base(next) { }

        public async override Task Invoke(IOwinContext context)
        {
            context.Response.Headers["MachineName"] = Environment.MachineName;
            await Next.Invoke(context);
        }
    }
    
    public class Startup
    {
        static OcrImageInfo ___downloadImage(OcrImageInfo ocr)
        {
            try
            {
                string file = Path.GetFileName(ocr.Url);
                ocr.FileName = file.Substring(0, file.Length - 4) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jpg";

                file = Path.Combine(Program.PATH_OCR_IMAGE, ocr.FileName);
                ImageFormat format = ImageFormat.Jpeg;

                WebClient client = new WebClient();
                Stream stream = client.OpenRead(ocr.Url);
                Bitmap bitmap = new Bitmap(stream);

                if (bitmap != null)
                {
                    bitmap.Save(file, format);
                }

                stream.Flush();
                stream.Close();
                client.Dispose();

                ocr.DownloadSuccess = true;
            }

            catch (Exception ex)
            {
                ocr.DownloadSuccess = false;
                ocr.StateOcr = STATE_OCR.OCR_FAIL_DOWNLOAD_FILE;
                ocr.TextError = ex.Message;
            }

            return ocr;
        }

        // This method is required by Katana:
        public void Configuration(IAppBuilder app)
        {
            // Adding to the pipeline with our own middleware
            app.Use(async (context, next) =>
            {
                // Add Header
                context.Response.Headers["Product"] = "Web Api and Owin Middleware";

                string file = context.Request.Query["file"];
                if (!string.IsNullOrWhiteSpace(file))
                {
                    OcrImageInfo ocr = new OcrImageInfo();
                    try
                    {
                        ocr.TextError = string.Empty;
                        ocr.TimeStart = long.Parse(DateTime.Now.ToString("yyyyMMddHHmmss"));


                        ocr.IsUrl = file.ToLower().StartsWith("http");
                        if (ocr.IsUrl) ocr.Url = file; else ocr.FileName = file;

                        if (ocr.IsUrl)
                            ocr = ___downloadImage(ocr);

                        ocr = Program.goo_ocr_uploadFile(ocr);
                    }
                    catch (Exception e1) {
                        ocr.TextError = e1.Message;
                    }
                    string json = ocr.app_getJsonResult();

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json";
                    context.Response.Write(json);
                }
                else
                {
                    // Call next middleware
                    await next.Invoke();
                }
            });

            // Custom Middleare
            app.Use(typeof(SimpleMiddleware));

            // Configure Web API for self-host.
            var config = ConfigureWebApi();

            // Web Api
            app.UseWebApi(config);

            ////////// File Server
            ////////var options = new FileServerOptions
            ////////{
            ////////    EnableDirectoryBrowsing = true,
            ////////    EnableDefaultFiles = true,
            ////////    DefaultFilesOptions = { DefaultFileNames = { "home.html" } },
            ////////    FileSystem = new PhysicalFileSystem("StaticFiles"),
            ////////    StaticFileOptions = { ContentTypeProvider = new FileExtensionContentTypeProvider() }
            ////////};
            ////////app.UseFileServer(options);


            if (!Directory.Exists("htdocs")) Directory.CreateDirectory("htdocs");

            var physicalFileSystem = new PhysicalFileSystem("./htdocs");

            // file server options
            var options = new FileServerOptions
            {
                EnableDefaultFiles = true,
                FileSystem = physicalFileSystem, // register file system
                EnableDirectoryBrowsing = false
            };

            options.StaticFileOptions.FileSystem = physicalFileSystem;
            options.StaticFileOptions.ServeUnknownFileTypes = true;
            options.DefaultFilesOptions.DefaultFileNames = new[] { "index.html" };
            //app.Use<DefaultFileRewriterMiddleware>(physicalFileSystem);  // middleware to direct non-existing file URLs to index.html
            app.UseFileServer(options);
        }

        private HttpConfiguration ConfigureWebApi()
        {
            var config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                "DefaultApi",
                "api/{controller}/{id}",
                new { id = RouteParameter.Optional });
            return config;
        }
    }
    
    public class OwinServer
    {
        readonly int _PORT = 0;
        public OwinServer(int PORT) { _PORT = PORT; }

        private IDisposable _webapp;

        public void Start()
        {
            _webapp = WebApp.Start<Startup>("http://127.0.0.1:" + _PORT.ToString());
        }

        public void Stop()
        {
            _webapp.Dispose();
        }
    }

    class Program
    {
        public const string PATH_OCR_IMAGE = @"C:\ocr-images\";

        public Program()
        {
            if (Directory.Exists(PATH_OCR_IMAGE) == false) Directory.CreateDirectory(PATH_OCR_IMAGE);
            if (Directory.Exists(PATH_OCR_IMAGE + "log") == false) Directory.CreateDirectory(PATH_OCR_IMAGE + "log");
        }

        #region [ GOOGLE SERVICE ]

        public STATE_GOO_SERVICE StateGooService { get; set; }

        const string fileKey = "key.json";
        static string[] Scopes = { DriveService.Scope.Drive };
        static string ApplicationName = "Ocr-Image-Driver-Vision";
        static UserCredential gooCredential = null;
        static DriveService gooService = null;

        static void goo_initCredentialService()
        {
            //_app.setTitleMessage(STATE_GOO_SERVICE.GOO_AUTHEN_PROCESSING.ToString());
            //_app.StateGooService = STATE_GOO_SERVICE.GOO_AUTHEN_PROCESSING;

            if (!File.Exists(fileKey))
            {
                //_app.StateGooService = STATE_GOO_SERVICE.GOO_AUTHEN_FAIL_MISS_KEY;
                //_app.setTitleMessage("ERROR: Cannot find file key.json");
                return;
            }

            try
            {
                using (var stream = new FileStream(fileKey, FileMode.Open, FileAccess.Read))
                {
                    //string credPath = Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
                    string credPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    //credPath = Path.Combine(credPath, ".credentials/" + ApplicationName);
                    var fileStore = new FileDataStore(credPath, true);

                    gooCredential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        Scopes, "user", CancellationToken.None, fileStore).Result;
                }

                ////Create Drive API service.
                gooService = new DriveService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = gooCredential,
                    ApplicationName = ApplicationName,
                });

                //_app.StateGooService = STATE_GOO_SERVICE.GOO_AUTHEN_SUCCESS;
                //_app.setTitleMessage(STATE_GOO_SERVICE.GOO_AUTHEN_SUCCESS.ToString());
            }
            catch (Exception ex)
            {
                //_app.StateGooService = STATE_GOO_SERVICE.GOO_AUTHEN_FAIL_INVALID_KEY;
                //_app.setTitleMessage("ERROR: " + ex.Message);
            }
        }

        public static OcrImageInfo goo_ocr_uploadFile(OcrImageInfo ocr)
        {
            string file = Path.Combine(PATH_OCR_IMAGE, ocr.FileName);
            if (File.Exists(file) == false)
            {
                ocr.StateOcr = STATE_OCR.OCR_FAIL_MISS_FILE;
                return ocr;
            }

            DataFile body = new DataFile()
            {
                Title = ocr.FileName,
                Description = ocr.Url,
                //body.MimeType = "application/vnd.ms-excel";
                MimeType = "image/jpeg"
            };

            byte[] byteArray = File.ReadAllBytes(file);
            using (MemoryStream stream = new MemoryStream(byteArray))
            {
                try
                {
                    //FilesResource.InsertMediaUpload request = service.Files.Insert(body, stream, "application/vnd.google-apps.spreadsheet");
                    FilesResource.InsertMediaUpload request = gooService.Files.Insert(body, stream, "application/vnd.google-apps.photo");
                    request.Ocr = true;
                    request.OcrLanguage = "vi";
                    request.Convert = true;

                    request.Upload();
                    DataFile imgFile = request.ResponseBody;
                    string fileId = imgFile.Id;

                    // Copy image and paste as document
                    var textMetadata = new DataFile();
                    //textMetadata.Name = inputFile.Name;
                    //textMetadata.Parents = new List<string> { folderId };
                    textMetadata.MimeType = "application/vnd.google-apps.document";
                    FilesResource.CopyRequest requestCopy = gooService.Files.Copy(textMetadata, fileId);
                    requestCopy.Fields = "id";
                    requestCopy.OcrLanguage = "vi";
                    var textFile = requestCopy.Execute();

                    // Now we export document as plain text
                    FilesResource.ExportRequest requestExport = gooService.Files.Export(textFile.Id, "text/plain");
                    string output = requestExport.Execute();

                    ocr.TextResult = output;
                    ocr.StateOcr = STATE_OCR.OCR_SUCCESS;

                    //writeLogMessage("OK: " + ocr.FileName);

                    if (ocr.WriteToFile)
                    {
                        if (!string.IsNullOrEmpty(ocr.TextResult))
                            File.WriteAllText(PATH_OCR_IMAGE + @"log\" + ocr.FileName + ".txt", ocr.TextResult);
                    }
                }
                catch (Exception e)
                {
                    ocr.TextError = e.Message;
                    ocr.StateOcr = STATE_OCR.OCR_FAIL_THROW_ERROR;
                }
            }

            return ocr;
        }

        #endregion


        static int _PORT = 0;
        static void Main(string[] args)
        {
            if (args.Length > 0) int.TryParse(args[0], out _PORT);
            if (_PORT == 0)
            {
                TcpListener l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                _PORT = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
            }

            ////////string baseUrl = "http://localhost:9090"; // webserver running on port 9090
            ////////using (WebApp.Start<Startup>(baseUrl))
            ////////{
            ////////    Console.WriteLine(baseUrl);
            ////////    Process.Start(baseUrl); // open the page from the application
            ////////    Console.WriteLine("Press Enter to quit.");
            ////////    Console.ReadKey();
            ////////}

            Console.WriteLine("Starting web Server...");
            HostFactory.Run(x =>
            {
                x.Service<OwinServer>(s =>
                {
                    s.ConstructUsing(name => new OwinServer(_PORT));
                    s.WhenStarted(tc =>
                    {
                        goo_initCredentialService();
                        
                        //// For test
                        //_app.goo_ocr_uploadFile();
                        //string result = _app.app_getJsonResult();

                        tc.Start();
                        Process.Start("http://localhost:" + _PORT.ToString());
                    });
                    s.WhenStopped(tc => tc.Stop());
                });
                x.RunAsLocalSystem();

                x.SetDescription("This is application of a Windows Service using Topshelf.");
                x.SetDisplayName("Self Host Web API and Owin Middleware");
                x.SetServiceName("AspNetSelfHostWithMiddleware");
            });

            Console.WriteLine("Server running at {0} - press Enter to quit. ", "http://localhost:" + _PORT.ToString());
            Console.ReadLine();

        }
    }
}
