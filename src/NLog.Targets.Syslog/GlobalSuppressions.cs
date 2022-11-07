// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Roslynator", "RCS1225:Make class sealed.", Justification = "<Pending>", Scope = "type", Target = "~T:NLog.Targets.Syslog.MessageSend.BackoffDelayProvider")]
[assembly: SuppressMessage("Roslynator", "RCS1229:Use async/await when necessary.", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.AsyncLogger.ProcessQueueAsync(NLog.Targets.Syslog.MessageCreation.MessageBuilder,System.Threading.Tasks.TaskCompletionSource{System.Object})~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Roslynator", "RCS1047:Non-asynchronous method name should not end with 'Async'.", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.AsyncLogger.ProcessQueueAsync(NLog.Targets.Syslog.MessageCreation.MessageBuilder)")]
[assembly: SuppressMessage("Roslynator", "RCS1229:Use async/await when necessary.", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.LogEventMsgSet.SendAsync(System.Threading.CancellationToken,System.Threading.Tasks.TaskCompletionSource{System.Object})~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Roslynator", "RCS1163:Unused parameter.", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.SyslogTarget.Init")]
[assembly: SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.LogEventMsgSet.SendAsync(System.Threading.CancellationToken,System.Threading.Tasks.TaskCompletionSource{System.Object})~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Roslynator", "RCS1225:Make class sealed.", Justification = "<Pending>", Scope = "type", Target = "~T:NLog.Targets.Syslog.Policies.Throttling")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.Policies.SplitOnNewLinePolicy.CausesSplitOf(System.String)~System.Boolean")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.Policies.InternalLogDuplicatesPolicy.IsApplicable~System.Boolean")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.Settings.NotifyPropertyChanged.CollectionChangedFactory(System.ComponentModel.PropertyChangedEventHandler)~System.Collections.Specialized.NotifyCollectionChangedEventHandler")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.Policies.InternalLogDuplicatesPolicy.Apply``1(System.Collections.Generic.IEnumerable{``0},System.Func{``0,System.String})")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.MessageSend.SocketInitialization.DiscardPendingDataOnClose(System.Net.Sockets.Socket)")]
[assembly: SuppressMessage("Style", "IDE0057:Use range operator", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.Policies.TruncateToKnownValuePolicy.Apply(System.String)~System.String")]
[assembly: SuppressMessage("Style", "IDE0063:Use simple 'using' statement", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.MessageSend.SocketInitializationForWindows.CanSetSockOptKeepAliveSetting(System.Action{System.Net.Sockets.Socket})~System.Boolean")]
[assembly: SuppressMessage("Style", "IDE0074:Use compound assignment", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.MessageSend.SocketInitializationForWindows.ApplyKeepAliveValues(System.Net.Sockets.Socket,NLog.Targets.Syslog.Settings.KeepAliveConfig)")]
[assembly: SuppressMessage("Roslynator", "RCS1058:Use compound assignment.", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.MessageSend.SocketInitializationForWindows.ApplyKeepAliveValues(System.Net.Sockets.Socket,NLog.Targets.Syslog.Settings.KeepAliveConfig)")]
[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "<Pending>", Scope = "member", Target = "~F:NLog.Targets.Syslog.MessageCreation.SdIdToInvalidParamNamePattern.InvalidIanaParamNames")]
[assembly: SuppressMessage("Style", "IDE0090:Use 'new(...)'", Justification = "<Pending>", Scope = "member", Target = "~F:NLog.Targets.Syslog.MessageCreation.SdElement.LogDuplicatesPolicy")]
[assembly: SuppressMessage("Style", "IDE0074:Use compound assignment", Justification = "<Pending>", Scope = "member", Target = "~M:NLog.Targets.Syslog.MessageCreation.MessageBuilder.BuildLogEntries(NLog.LogEventInfo,NLog.Layouts.Layout)~System.String[]")]
