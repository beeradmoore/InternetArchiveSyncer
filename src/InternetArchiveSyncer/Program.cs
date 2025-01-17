using ByteSizeLib;
using CliWrap;
using CliWrap.Buffered;
//using Downloader;
using InternetArchiveSyncer;
using Serilog;
using System.Text.Json;
using System.Xml;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

Config? config = null;

var configFile = "config.json";
if (File.Exists(configFile) == false)
{
    config = new Config();
    config.AccessKey = "your_access_key";
    config.Secret = "your_secret";
    config.ArchiveConfigs.Add(new ArchiveConfig()
    {
        Archive = "example-1",
        CustomPath = "C:\\ia_sync\\example_one\\",
        Enabled = false,
    });
    config.ArchiveConfigs.Add(new ArchiveConfig()
    {
        Archive = "example-2",
        CustomPath = "C:\\ia_sync\\example_two\\",
        Enabled = false,
    });
    var jsonData = JsonSerializer.Serialize(config, new JsonSerializerOptions() {  WriteIndented = true });
    File.WriteAllText(configFile, jsonData);
    Console.WriteLine($"Config file {configFile} was not found. This has been created. Plesae adjust its values and run again.");
    return;
}

config = JsonSerializer.Deserialize<Config>(File.ReadAllText(configFile));
if (config is null)
{
    Log.Error($"Could not load {configFile}.");
    return;
}


var httpClientHandler = new HttpClientHandler()
{
    UseCookies = true,
    AllowAutoRedirect = true,
    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
};
var httpClient = new HttpClient(httpClientHandler);
httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0");


var filesToDownload = new List<IAFileNode>();
ulong totalTotalSize = 0;
var fileHelper = new FileHelper();


var baseOutputDirectory = "output";
if (Directory.Exists(baseOutputDirectory) == false)
{
    Directory.CreateDirectory(baseOutputDirectory);
}

/*
var downloadHeaders = new System.Net.WebHeaderCollection();
//downloadHeaders.Add(System.Net.HttpRequestHeader.Authorization, "LOW " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{config.AccessKey}:{config.Secret}")));
downloadHeaders.Add(System.Net.HttpRequestHeader.Authorization, $"LOW {config.AccessKey}:{config.Secret}");


var downloadOptions = new DownloadConfiguration()
{
    ChunkCount = 4, // Number of file parts, default is 1
    ParallelDownload = true, // Download parts in parallel (default is false),
    RequestConfiguration = new RequestConfiguration()
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
        Headers = downloadHeaders,
    },
};
var downloader = new DownloadService(downloadOptions);
downloader.DownloadStarted += (object? sender, DownloadStartedEventArgs e) =>
{
    Console.WriteLine("DownloadStarted");
};
downloader.DownloadProgressChanged += (object? sender, DownloadProgressChangedEventArgs e) =>
{
    Console.WriteLine("DownloadProgressChanged");
};
downloader.DownloadFileCompleted += (object? sender, System.ComponentModel.AsyncCompletedEventArgs e) =>
{
    Console.WriteLine("DownloadFileCompleted");
};
downloader.ChunkDownloadProgressChanged += (object? sender, DownloadProgressChangedEventArgs e) =>
{
    Console.WriteLine("ChunkDownloadProgressChanged");
};

System.Net.ServicePointManager.Expect100Continue = true;
*/

foreach (var archive in config.ArchiveConfigs)
{
    if (archive.Enabled == false)
    {
        continue;
    }

    var outputDirectory = Path.Combine(baseOutputDirectory, archive.Archive);

    if (string.IsNullOrEmpty(archive.CustomPath) == false)
    {
        outputDirectory = archive.CustomPath;
    }

    if (Directory.Exists(outputDirectory) == false)
    {
        Directory.CreateDirectory(outputDirectory);
    }

    var xmlFilesUrl = $"https://archive.org/download/{archive.Archive}/{archive.Archive}_files.xml";
    var xmlFilesPath = Path.Combine(baseOutputDirectory, Path.GetFileName(xmlFilesUrl));

    if (File.Exists(xmlFilesPath) == false)
    {
        Log.Information($"Downloading: {xmlFilesUrl}");
        using (var stream = await httpClient.GetStreamAsync(xmlFilesUrl))
        {
            using (var fileStream = File.Create(xmlFilesPath))
            {
                stream.CopyTo(fileStream);
            }
        }
    }
    else
    {
        Log.Information($"Checking: {xmlFilesUrl}");
    }

    var doc = new XmlDocument();
    doc.Load(xmlFilesPath);
    var filesNode = doc.DocumentElement?.SelectSingleNode("/files");
    if (filesNode is not null)
    {
        ulong totalSize = 0;
        IAFileNode? lastUpdatedIAFileNode = null;
        int totalFiles = 0;
        //foreach (XmlNode fileNode in filesNode.ChildNodes)

        var options = new ParallelOptions()
        {
            MaxDegreeOfParallelism = 8
        };

        var childNodes = new List<XmlNode>();
        foreach (XmlNode childNode in filesNode.ChildNodes)
        {
            childNodes.Add(childNode);
        }

        var breakLoop = false;

        await Parallel.ForEachAsync(childNodes, options, async (XmlNode fileNode, CancellationToken cancellationToken) =>
        {
            if (breakLoop)
            {
                return;
            }

            try
            {
                var iaFileNode = IAFileNode.FromXmlNode(fileNode);
                totalSize += (ulong)iaFileNode.Size;
                if (lastUpdatedIAFileNode is null || iaFileNode.ModifiedTime > lastUpdatedIAFileNode?.ModifiedTime)
                {
                    var extension = Path.GetExtension(iaFileNode.Filename).ToLower();

                    // Can probably me more explicity to ignore files specific to this.
                    if (extension != ".sqlite" && extension != ".xml" && extension != ".jpg" && extension != ".png")
                    {
                        lastUpdatedIAFileNode = iaFileNode;
                    }
                }
                filesToDownload.Add(iaFileNode);
                ++totalFiles;


                var urlSubPath = string.Empty;
                var outputFile = Path.Combine(outputDirectory, iaFileNode.Filename);
                if (string.IsNullOrEmpty(iaFileNode.Path) == false)
                {
                    var newOutputDirectory = Path.Combine(outputDirectory, iaFileNode.Path);
                    if (Directory.Exists(newOutputDirectory) == false)
                    {
                        Directory.CreateDirectory(newOutputDirectory);
                    }
                    outputFile = Path.Combine(newOutputDirectory, iaFileNode.Filename);

                    var iaFileNodePath = iaFileNode.Path;
                    if (iaFileNodePath.Contains("\\"))
                    {
                        iaFileNodePath = iaFileNodePath.Replace("\\", "/");
                    }
                    urlSubPath = "/" + Uri.EscapeDataString(iaFileNodePath);
                }

                if (iaFileNode.Path.Contains("\\"))
                {
                    //Debugger.Break();
                }
                var urlFilename = Uri.EscapeDataString(iaFileNode.Filename);

                var downloadUrl = $"https://archive.org/download/{archive.Archive}{urlSubPath}/{urlFilename}";

                var shouldDownload = true;

                if (File.Exists(outputFile) == true)
                {
                    var computedMD5 = fileHelper.GetMD5(outputFile);
                    if (computedMD5 == iaFileNode.MD5)
                    {
                        shouldDownload = false;
                    }
                    else
                    {
                        Log.Error($"Hash does not match, deleting {outputFile}.");
                        File.Delete(outputFile);
                    }
                }

                if (shouldDownload == true)
                {
                    var startTime = DateTime.Now;
                    Log.Information($"Downloading: {downloadUrl}");

                    var fullPath = Path.GetFullPath(outputFile);

                    //await downloader.DownloadFileTaskAsync(downloadUrl);

                    var tempPath = Path.GetTempFileName();

                    var result = await Cli.Wrap("curl")
                        .WithArguments(["--location-trusted", "--parallel", "--output", tempPath, "--header", $"authorization: LOW {config.AccessKey}:{config.Secret}", downloadUrl])
                        .ExecuteBufferedAsync();
                    
                    var duration = DateTime.Now - startTime;
                    Log.Information($"Duration: {duration}");
                    var computedMD5 = fileHelper.GetMD5(tempPath);
                    if (computedMD5 == iaFileNode.MD5)
                    {
                        File.Move(tempPath, outputFile, true);
                    }
                    else
                    {
                        Log.Error($"MD5 for {outputFile} is not what is expected. Deleting.");
                        File.Delete(tempPath);
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err, "Exception thrown");
                breakLoop = true;
            }
        });


        var totalByteSize = ByteSize.FromBytes(totalSize);
        Log.Information($"Total files: {totalFiles}");
        if (lastUpdatedIAFileNode is not null)
        {
            var latestUpdatedDateTime = DateTimeOffset.FromUnixTimeSeconds(lastUpdatedIAFileNode.ModifiedTime);
            Log.Information($"Latest updated: {lastUpdatedIAFileNode?.Filename}, {latestUpdatedDateTime.DateTime.ToString()}");
        }
        Log.Information($"Total size: {totalByteSize}");

        Log.Information("");

        totalTotalSize += totalSize;

    }


}


var totalTotalByteSize = ByteSize.FromBytes(totalTotalSize);
Log.Information("");
Log.Information($"Entire sync size: {totalTotalByteSize}");
Log.CloseAndFlush();