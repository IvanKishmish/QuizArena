using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using QuizArena.Persistence.Mongo.Documents;

namespace QuizArena.Persistence.Mongo;

public static class MongoClassMapConfiguration
{
    public static void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(QuestionDocument)))
            return;

        BsonClassMap.RegisterClassMap<AnswerOptionDocument>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });

        BsonClassMap.RegisterClassMap<QuestionDocument>(cm =>
        {
            cm.AutoMap();

            cm.MapIdProperty(x => x.Id)
                .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

            cm.GetMemberMap(x => x.QuizSetId)
                .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));

            cm.SetIgnoreExtraElements(true);
        });
    }
}
