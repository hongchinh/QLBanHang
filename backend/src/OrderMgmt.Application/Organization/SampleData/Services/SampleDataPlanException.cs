namespace OrderMgmt.Application.Organization.SampleData.Services;

/// A plan the runner cannot carry out: a key the plan never defined, a missing requester, a branch that is not demo,
/// an unsupported step, a generator failure. Its message is user-facing and goes into SampleDataError as is.
public sealed class SampleDataPlanException : Exception
{
    public SampleDataPlanException(string message, Exception? innerException = null) : base(message, innerException) { }

    /// A generator failure (key KE-HOACH) keeps the generator's own message.
    public static SampleDataPlanException GeneratorFailed(Exception ex) => new(ex.Message, ex);
}
