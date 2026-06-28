namespace c4_api.pcgFlowMatching;

// State: error response body, used for not-found (404) and error (500) responses.
public class ErrorHttpOut
{
    public string Type = "ErrorHttpOut";
    public string Message;
}
