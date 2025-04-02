using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace TinyInsights;

public class Crash
{
    public Crash()
    {
        Message = string.Empty;
        ExceptionType = string.Empty;
    }

    public Crash(Exception exception)
    {
        var telem = new ExceptionTelemetry(exception);
        // using the AI serializer which does some other sanitization
        ExceptionTelemetry = Convert.ToBase64String(
            Microsoft.ApplicationInsights.Extensibility.Implementation.JsonSerializer.Serialize(new ITelemetry[]
                { telem }));
    }

    public string? ExceptionTelemetry { get; init; }
    
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public string? ExceptionType { get; init; }
    public string? ExceptionAssembly { get; init; }
    public string? Source { get; init; }

    public ExceptionTelemetry? GetExceptionTelemetry()
    {
        if (ExceptionTelemetry != null)
        {
            var json = Microsoft.ApplicationInsights.Extensibility.Implementation.JsonSerializer.Deserialize(
                Convert.FromBase64String(ExceptionTelemetry));
            var telems = JsonSerializer.Deserialize<IEnumerable<ExceptionTelemetry>>(json);
            // we only expect one per crash object
            return telems.FirstOrDefault();
        }
        else
        {
            return new ExceptionTelemetry(GetException());
        }
    }

    public Exception? GetException()
    {

        try
        {
            if (ExceptionType != null)
            {
                Trace.WriteLine("TinyInsights: This exception was stored as a telemetry message and cannot be deserialized to the original type");
                return null;
            }

            Assembly assembly = Assembly.Load(ExceptionAssembly);
            Type type = assembly.GetType(ExceptionType)!;

            Exception? ex;

            try
            {
                ex = Activator.CreateInstance(type, args: Message) as Exception;
            }
            catch (MissingMethodException)
            {
                ex = Activator.CreateInstance(type) as Exception;
            }

            if (ex is not null)
            {
                ex.Source = Source;
            }

            return ex;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"TinyInsights: Error restoring stored exception {ex}");
            return null;
        }
    }
}