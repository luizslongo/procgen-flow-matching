using System;
using System.Net;

namespace c4_api.pcgFlowMatching;

// Action: single-threaded HTTP server built on the BCL HttpListener. Owns the
// listener and holds injected references to the engine and store (explicit
// dependency passing, no singleton, no DI container). It processes one request
// at a time, which also serializes the TorchSharp generation calls. There is no
// async/await and no lock: the blocking GetContext loop IS the event loop
// (single-threaded-event-loop standard). The listener is created in Init(), not
// the constructor (deferred-initialization standard).
public class HttpServer
{
    public ApiConfig Config;
    public GenerationEngine Engine;
    public GenerationStoreInterface Store;

    HttpListener _listener;
    bool _isRunning;

    public void Init()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(Config.ServerUrl);
        _isRunning = false;
    }

    public void Run()
    {
        _listener.Start();
        _isRunning = true;
        Console.WriteLine("[c4-api] listening on " + Config.ServerUrl);

        while (_isRunning)
        {
            HttpListenerContext context = _listener.GetContext();
            // One try/catch at the request boundary keeps the event loop alive and
            // converts any unexpected failure (including third-party throws from
            // TorchSharp) into a generic 500 with no internal detail leaked.
            try
            {
                DispatchRequest(context);
            }
            catch (Exception)
            {
                HttpResponseWriter.WriteError(context, 500, "internal error");
            }
        }
    }

    void DispatchRequest(HttpListenerContext context)
    {
        string method = context.Request.HttpMethod;
        string path = context.Request.Url.AbsolutePath;
        Console.WriteLine("[c4-api] " + method + " " + path);

        if (method == "GET" && path == "/status/health")
        {
            HealthHandler.Handle(context, Engine);
            return;
        }
        if (method == "POST" && path == "/pcg-entity-generation/add")
        {
            GenerationAddHandler.Handle(context, Engine, Store);
            return;
        }
        if (method == "GET" && path == "/pcg-entity-generation/get")
        {
            GenerationGetHandler.Handle(context, Store);
            return;
        }
        if (method == "GET" && path == "/pcg-entity-generation/list")
        {
            GenerationListHandler.Handle(context, Store);
            return;
        }
        HttpResponseWriter.WriteError(context, 404, "no route for " + method + " " + path);
    }
}
