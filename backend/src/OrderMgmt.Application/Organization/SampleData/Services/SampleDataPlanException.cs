namespace OrderMgmt.Application.Organization.SampleData.Services;

/// A plan the runner cannot carry out: a key the plan never defined, a missing requester, a branch that is not demo,
/// an unsupported step, a generator failure. Its message is user-facing and goes into SampleDataError as is.
public sealed class SampleDataPlanException : Exception
{
    private const string GeneratorFailedMessage =
        "Không tạo được kế hoạch dữ liệu mẫu. Hãy xóa chi nhánh demo và tạo lại.";

    public SampleDataPlanException(string message, Exception? innerException = null) : base(message, innerException) { }

    /// A generator failure (key KE-HOACH) shows a fixed text: every signed-in user reads SampleDataError, so the
    /// generator's own message stays in the inner exception, which the runner logs.
    public static SampleDataPlanException GeneratorFailed(Exception ex) => new(GeneratorFailedMessage, ex);
}
