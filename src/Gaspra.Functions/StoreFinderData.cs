using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TinyCsvParser;
using TinyCsvParser.Mapping;

namespace Gaspra.Functions;

public class StoreFinderData
{
    public string GoToDatabase { get; set; }
    public string StoreName { get; set; }
    public float StoreLatitude { get; set; }
    public float StoreLongitude { get; set; }
    public string StoreDisplayName { get; set; }
    
}

public class StoreFinderDataMapping : CsvMapping<StoreFinderData>
{
    public StoreFinderDataMapping() : base()
    {
        MapProperty(0, x => x.GoToDatabase);
        MapProperty(1, x => x.StoreName);
        MapProperty(2, x => x.StoreLatitude);
        MapProperty(3, x => x.StoreLongitude);
        MapProperty(4, x => x.StoreDisplayName);
    }
}

public class StoreFinderDataConverter
{
    public async Task ConvertCsv()
    {
        var csvFilePath = $"{Directory.GetCurrentDirectory()}/StoreFinderData.csv";

        var csvParserOptions = new CsvParserOptions(true, ',');

        var csvMapper = new StoreFinderDataMapping();

        var csvParser = new CsvParser<StoreFinderData>(csvParserOptions, csvMapper);

        var storeData = csvParser
            .ReadFromFile(csvFilePath, Encoding.Default)
            .ToList();
    }
}