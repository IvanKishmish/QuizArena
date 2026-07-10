using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;

namespace QuizArena.Persistence.Mongo;

public static class MongoClassMapConfiguration
{
    public static void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Question)))
            return;

        BsonClassMap.RegisterClassMap<Question>(cm =>
        {
            cm.AutoMap();
            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new MongoDB.Bson.Serialization.Serializers.GuidSerializer(GuidRepresentation.Standard));
            
            cm.UnmapProperty(x => x.Options);
            cm.MapField("_options").SetElementName("options");
            
            cm.SetIgnoreExtraElements(true);
        });

        BsonClassMap.RegisterClassMap<AnswerOptionParams>(cm =>
        {
            cm.AutoMap();
        });
    }
}