namespace c4_api.pcgFlowMatching;

// State: detail for a recoverable error returned by an action method. A null
// ErrorReturn means success; a non-null ErrorReturn carries the failure message.
// Local stand-in for the kcg-lib ErrorReturn type (error-return-pattern standard).
public class ErrorReturn
{
    public string Message;
}
