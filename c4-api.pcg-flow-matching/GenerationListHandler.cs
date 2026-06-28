using System.Collections.Generic;
using System.Net;

namespace c4_api.pcgFlowMatching;

// Handler: GET /pcg-entity-generation/list
// Usage:
//   GET /pcg-entity-generation/list
//   Response (200): { "Type": "GenerationListHttpOut", "Generations": [ ... ], "TotalCount": N }
public class GenerationListHandler
{
    public static void Handle(HttpListenerContext context, GenerationStoreInterface store)
    {
        List<GenerationHttpOut> records = store.ListGenerations();
        GenerationListHttpOut response = new GenerationListHttpOut();
        response.Generations = records;
        response.TotalCount = records.Count;
        HttpResponseWriter.WriteJson(context, 200, response);
    }
}
