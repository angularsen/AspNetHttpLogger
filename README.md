AspNetHttpLogger
================
Log raw HTTP requests and responses with ASP.NET WebApi and WebApi2.

### Step 1 - Install nuget
To install AspNetHttpLogger, run the following command in the [Package Manager Console](http://docs.nuget.org/docs/start-here/using-the-package-manager-console)
![nuget-installpackage](https://cloud.githubusercontent.com/assets/787816/4447419/adb615a8-480a-11e4-81a0-bd29c231ef4c.png)

### Step 2 - Register LoggingHandler as a message handler
```csharp
var loggingHandler = new LoggingHandler();
loggingHandler.ResponseCompleted += DoSomething;

GlobalConfiguration.Configuration.MessageHandlers.Add(loggingHandler);
```

###Example code###
**Global.asax.cs**
```csharp
public class MvcApplication : System.Web.HttpApplication
{
    public void Application_Start()
    {
        // Other setup stuff to peform on startup..
        GlobalConfiguration.Configure(EnableRequestResponseLogging);
    }

    private void EnableRequestResponseLogging(HttpConfiguration httpConfiguration)
    {
        var loggingHandler = new LoggingHandler();
        
        // Hook up events
        loggingHandler.InternalError += LoggingHandlerOnInternalError;
        loggingHandler.ResponseCompleted += LoggingHandlerOnResponseCompleted;

        // Register as a message handler to peek at all requests and responses
        httpConfiguration.MessageHandlers.Add(loggingHandler);
    }

    /// <summary>
    /// A request/response just completed. See <see cref="logEvent"/> for more details.
    /// </summary>
    /// <param name="logEvent">Details about the request and response. Call <see cref="LogEvent.ToString"/> for a pre-formatted string output.</param>
    private void LoggingHandlerOnResponseCompleted(LogEvent logEvent)
    {
        Log.Debug(logEvent.Summary + "\n" + logEvent);
        
        // Log the raw request/response output to our Loggr logging service
        var loggr = DependencyResolver.Current.GetService<ILoggrService>();
        loggr.PostEvent(logEvent.Summary, logEvent.UserName, logEvent.Request, new[] {"api", "log", "raw"}, logEvent.ToString(),
            LoggrDataType.PlainText);
    }

    /// <summary>
    /// An exception occurred in the <see cref="LoggingHandler"/>. It will be silently ignored,
    /// but you can use this event to log and track down issues with the handler.
    /// </summary>
    /// <param name="response">The <see cref="HttpResponseMessage"/>.</param>
    /// <param name="ex">Exception that occurred.</param>
    private void LoggingHandlerOnInternalError(HttpResponseMessage response, Exception ex)
    {
        Log.ErrorException("Exception occurred in LoggingHandler.", ex);

        // Send information about the error to our Raygun exception tracking service
        var raygun = DependencyResolver.Current.GetService<IRaygunService>();
        raygun.SendAsync(ex, new[] {"LoggingHandler"});

        // Log the exception
        var loggr = DependencyResolver.Current.GetService<ILoggrService>();
        loggr.PostErrorEvent(ex, "LoggingHandler: " + ex.Message, User.Identity.Name, response.RequestMessage,
            new[] {"api", "exception", "handled"});
    }
}
```

###Feedback
This is so far the best way I have found to accomplish this in ASP.NET WebApi and WebApi2. If you know of a better way or have any improvements, please sound off in the Issues page.
