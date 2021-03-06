using System;
using AutoTest.Core.TestRunners;
using System.Collections.Generic;
using AutoTest.Core.DebugLog;
namespace AutoTest.Core.Messaging.MessageConsumers
{
	public class AssemblyChangeConsumer : IConsumerOf<AssemblyChangeMessage>
	{
		private ITestRunner[] _testRunners;
		private IMessageBus _bus;
		private IDetermineIfAssemblyShouldBeTested _testAssemblyValidator;
		
		public AssemblyChangeConsumer(ITestRunner[] testRunners, IMessageBus bus, IDetermineIfAssemblyShouldBeTested testAssemblyValidator)
		{
			_testRunners = testRunners;
			_bus = bus;
			_testAssemblyValidator = testAssemblyValidator;
		}
		
		#region IConsumerOf[AssemblyChangeMessage] implementation
		public void Consume (AssemblyChangeMessage message)
		{
			informParticipants(message);
			var runReport = new RunReport();
			foreach (var runner in _testRunners)
				runTest(runner, message, runReport);
			_bus.Publish(new RunFinishedMessage(runReport));
		}
		#endregion
		
		private void informParticipants(AssemblyChangeMessage message)
		{
			Debug.ConsumingAssemblyChangeMessage(message);
            _bus.Publish(new RunStartedMessage(message.Files));
		}
		
		private void runTest(ITestRunner runner, AssemblyChangeMessage message, RunReport report)
		{
			var runInfos = new List<TestRunInfo>();
			foreach (var assembly in message.Files)
			{
				if (_testAssemblyValidator.ShouldNotTestAssembly(assembly.FullName))
					return;
				if (runner.CanHandleTestFor(assembly))
					runInfos.Add(new TestRunInfo(null, assembly.FullName));
			}
			if (runInfos.Count == 0)
				return;
			var results = runner.RunTests(runInfos.ToArray());
			mergeReport(results, report);
		}
		
		private void mergeReport(TestRunResults[] results, RunReport report)
		{
			foreach (var result in results)
			{
	            report.AddTestRun(
	                result.Project,
	                result.Assembly,
	                result.TimeSpent,
	                result.Passed.Length,
	                result.Ignored.Length,
	                result.Failed.Length);
	            _bus.Publish(new TestRunMessage(result));
			}
		}
	}
}

