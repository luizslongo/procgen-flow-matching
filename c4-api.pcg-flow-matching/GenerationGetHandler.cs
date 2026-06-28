using System.Net;

namespace c4_api.pcgFlowMatching;

// Handler: GET /pcg-entity-generation/get?id=<id>
// Usage:
//   GET /pcg-entity-generation/get?id=ab12...
//   Response (200): GenerationHttpOut
//   Response (404): { "Type": "ErrorHttpOut", "Message": "generation not found: ..." }
public class GenerationGetHandler
{
    public static void Handle(HttpListenerContext context, GenerationStoreInterface store)
    {
        string id = context.Request.QueryString.Get("id");
        if (id == null || id.Length == 0)
        {
            HttpResponseWriter.WriteError(context, 500, "query parameter 'id' is required");
            return;
        }
        GenerationHttpOut record = store.GetGeneration(id);
        if (record == null)
        {
            HttpResponseWriter.WriteError(context, 404, "generation not found: " + id);
            return;
        }
        HttpResponseWriter.WriteJson(context, 200, record);
    }
}
