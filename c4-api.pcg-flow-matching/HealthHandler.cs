using System.Net;

namespace c4_api.pcgFlowMatching;

// Handler: GET /status/health
// Usage:
//   GET /status/health
//   Response (200): { "Type": "HealthHttpOut", "Status": "ok", "IsModelReady": true }
public class HealthHandler
{
    public static void Handle(HttpListenerContext context, GenerationEngine engine)
    {
        HealthHttpOut response = new HealthHttpOut();
        response.Status = "ok";
        response.IsModelReady = engine.IsModelAvailable();
        HttpResponseWriter.WriteJson(context, 200, response);
    }
}
