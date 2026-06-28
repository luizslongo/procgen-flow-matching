using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;

namespace c4_api.pcgFlowMatching;

// Action: serializes a response payload to indented JSON and writes it to the
// HTTP response with the given status code. Only 200, 404, and 500 are used
// across the API (http-status-codes standard). Public fields are serialized via
// IncludeFields, and output is pretty-printed (pretty-print-json standard).
public class HttpResponseWriter
{
    public static void WriteJson(HttpListenerContext context, int statusCode, object payload)
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.IncludeFields = true;
        options.WriteIndented = true;

        string json = JsonSerializer.Serialize(payload, payload.GetType(), options);
        byte[] bytes = Encoding.UTF8.GetBytes(json);

        // Security headers: blank the Server banner (HttpListener re-adds a versioned
        // Server header on send, so Remove() is not enough — overwrite it to empty),
        // forbid MIME sniffing, and mark JSON as non-cacheable.
        context.Response.Headers["Server"] = "";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Cache-Control"] = "no-store";

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.LongLength;
        Stream output = context.Response.OutputStream;
        output.Write(bytes, 0, bytes.Length);
        output.Close();
    }

    public static void WriteError(HttpListenerContext context, int statusCode, string message)
    {
        ErrorHttpOut error = new ErrorHttpOut();
        error.Message = message;
        WriteJson(context, statusCode, error);
    }
}
