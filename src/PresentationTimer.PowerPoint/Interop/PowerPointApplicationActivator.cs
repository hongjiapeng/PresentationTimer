using System.Reflection;
using System.Runtime.InteropServices;

namespace PresentationTimer.PowerPoint.Interop;

internal enum ApplicationActivationStatus
{
    Activated,
    Unavailable,
    Failed,
}

internal sealed record ApplicationActivationResult
{
    public ApplicationActivationResult(ApplicationActivationStatus status, object? instance, int hResult)
    {
        this.Status = status;
        this.Instance = instance;
        this.HResult = hResult;
    }

    public ApplicationActivationStatus Status { get; }

    public object? Instance { get; }

    public int HResult { get; }
}

/// <summary>Activates a registered PowerPoint automation application.</summary>
internal interface IPowerPointApplicationActivator
{
    /// <summary>Activates the application registered under the supplied ProgID.</summary>
    /// <param name="programmaticIdentifier">The COM programmatic identifier.</param>
    /// <returns>A categorized activation result.</returns>
    ApplicationActivationResult Activate(string programmaticIdentifier);
}

internal sealed class PowerPointApplicationActivator : IPowerPointApplicationActivator
{
    private const int ClassNotRegistered = unchecked((int)0x80040154);
    private const int InvalidClassString = unchecked((int)0x800401F3);

    public ApplicationActivationResult Activate(string programmaticIdentifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(programmaticIdentifier);

        try
        {
            Type? applicationType = Type.GetTypeFromProgID(programmaticIdentifier, throwOnError: false);
            if (applicationType is null)
            {
                return new ApplicationActivationResult(
                    ApplicationActivationStatus.Unavailable,
                    null,
                    ClassNotRegistered);
            }

            object? instance = Activator.CreateInstance(applicationType);
            return instance is null
                ? new ApplicationActivationResult(ApplicationActivationStatus.Failed, null, 0)
                : new ApplicationActivationResult(ApplicationActivationStatus.Activated, instance, 0);
        }
        catch (COMException exception) when (exception.HResult is ClassNotRegistered or InvalidClassString)
        {
            return new ApplicationActivationResult(
                ApplicationActivationStatus.Unavailable,
                null,
                exception.HResult);
        }
        catch (COMException exception)
        {
            return new ApplicationActivationResult(
                ApplicationActivationStatus.Failed,
                null,
                exception.HResult);
        }
        catch (Exception exception) when (exception is MemberAccessException or
            MissingMethodException or
            TargetInvocationException)
        {
            return new ApplicationActivationResult(
                ApplicationActivationStatus.Failed,
                null,
                exception.HResult);
        }
    }
}
