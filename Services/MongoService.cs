using System.IO.Enumeration;
using MongoDB.Bson;
using MongoDB.Driver;

namespace NoteQuery.Services;

public class MongoService
{
    private MongoClient _dbClient;
    private IMongoDatabase _db;
    private IMongoCollection<BsonDocument> _collection;
    // Parameterize these
    private string _dbName = "NoteQuery";
    private string _collectionName = "Notes";

    public MongoService()
    {
        // Gotta connect to local mongo in here
        _dbClient = new MongoClient("mongodb://localhost:27017/");
        var dbList = _dbClient.ListDatabaseNames().ToList();
        _db = _dbClient.GetDatabase(_dbName);
        _collection = _db.GetCollection<BsonDocument>(_collectionName);
        Console.WriteLine("The list of databases are: ");
        foreach (var db in dbList)
        {
            Console.WriteLine(db);
        }
    }

    public void UpdateDocument(string path)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("FileName", path);
        var file = "";
        using (StreamReader sr = new StreamReader(path))
        {
            file = sr.ReadToEnd();
        }
        var creationTime = File.GetCreationTime(path);
        var update = Builders<BsonDocument>.Update
            .Set("FileContents", file)
            .Set("UpdatedTime", DateTime.Now);
        _collection.UpdateOne(filter, update);
    }

    public void InsertDocument(string path)
    {
        var file = "";
        using (StreamReader sr = new StreamReader(path))
        {
            file = sr.ReadToEnd();
        }
        var creationTime = File.GetCreationTime(path);
        var document = new BsonDocument
        {
            { "FileName", path },
            { "FileContents", file },
            { "CreationTime", creationTime },
            { "UpdatedTime", DateTime.Now }
        };
        // Change this to async later
        _collection.InsertOne(document);
        
        
    }
}

