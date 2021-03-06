using AutoTest.Core.Caching.Projects;
using AutoTest.Core.Messaging.MessageConsumers;
using AutoTest.Core.FileSystem;
namespace AutoTest.Core.TestRunners
{
    public interface ITestRunner
    {
        bool CanHandleTestFor(ProjectDocument document);
		bool CanHandleTestFor(ChangedFile assembly);
        TestRunResults[] RunTests(TestRunInfo[] runInfos);
    }
}