using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using QuizArena.Domain.Common;
using QuizArena.Domain.Entities;
using QuizArena.Domain.Entities.Models;
using QuizArena.Persistence.Mongo.Documents;

namespace QuizArena.Persistence.Mongo;

public static class MongoClassMapConfiguration
{
    public static void Configure()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Question)))
            return;

        if (!BsonClassMap.IsClassMapRegistered(typeof(Entity)))
        {
            BsonClassMap.RegisterClassMap<Entity>(cm =>
            {
                cm.AutoMap();
                cm.SetIsRootClass(true);
                cm.MapIdProperty(x => x.Id)
                    .SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
            });
        }

        BsonClassMap.RegisterClassMap<Question>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });

        BsonClassMap.RegisterClassMap<AnswerOptionParams>(cm =>
        {
            cm.AutoMap();
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