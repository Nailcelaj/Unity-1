﻿using System.Collections.Generic;
using System.Threading;
using FluentAssertions;
using GitHub.Unity;
using NSubstitute;
using NUnit.Framework;
using TestUtils;
using TestUtils.Events;

namespace IntegrationTests
{
    class RepositoryManagerTests : BaseGitEnvironmentTest
    {
        [Test]
        public void ShouldDetectFileChanges()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            var expected = new GitStatus {
                Behind = 1,
                LocalBranch = "master",
                RemoteBranch = "origin/master",
                Entries =
                    new List<GitStatusEntry> {
                        new GitStatusEntry("foobar.txt", TestRepoMasterCleanSynchronized.Combine("foobar.txt"),
                            "foobar.txt", GitFileStatus.Untracked)
                    }
            };

            var result = new GitStatus();
            RepositoryManager.OnRepositoryChanged += status => { result = status; };

            Logger.Trace("Issuing Changes");

            var foobarTxt = TestRepoMasterCleanSynchronized.Combine("foobar.txt");
            foobarTxt.WriteAllText("foobar");

            Thread.Sleep(4000);

            Logger.Trace("Continue test");

            repositoryManagerListener.Received().OnRepositoryChanged(Args.GitStatus);
            result.AssertEqual(expected);

            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);

            //Assert.AreEqual(3, repositoryManagerListener.ReceivedCalls().Count());
        }

        [Test]
        public void ShouldAddAndCommitFiles()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            var expectedAfterChanges = new GitStatus {
                Behind = 1,
                LocalBranch = "master",
                RemoteBranch = "origin/master",
                Entries =
                    new List<GitStatusEntry> {
                        new GitStatusEntry("Assets\\TestDocument.txt",
                            TestRepoMasterCleanSynchronized.Combine("Assets", "TestDocument.txt"),
                            "Assets\\TestDocument.txt", GitFileStatus.Modified),
                        new GitStatusEntry("foobar.txt", TestRepoMasterCleanSynchronized.Combine("foobar.txt"),
                            "foobar.txt", GitFileStatus.Untracked)
                    }
            };

            var expectedAfterCommit = new GitStatus {
                Ahead = 1,
                Behind = 1,
                LocalBranch = "master",
                RemoteBranch = "origin/master",
                Entries = new List<GitStatusEntry>()
            };

            ManualResetEvent evt = new ManualResetEvent(false);
            var result = new GitStatus();
            RepositoryManager.OnRepositoryChanged += status => {
                result = status;
                evt.Set();
            };

            Logger.Trace("Issuing Changes");

            var foobarTxt = TestRepoMasterCleanSynchronized.Combine("foobar.txt");
            foobarTxt.WriteAllText("foobar");

            var testDocumentTxt = TestRepoMasterCleanSynchronized.Combine("Assets", "TestDocument.txt");
            testDocumentTxt.WriteAllText("foobar");

            Assert.True(evt.WaitOne(5000));
            evt.Reset();

            Logger.Trace("Continue test");

            repositoryManagerListener.Received().OnRepositoryChanged(Args.GitStatus);
            result.AssertEqual(expectedAfterChanges);

            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);

            repositoryManagerListener.ClearReceivedCalls();

            Logger.Trace("Issuing Command");

            RepositoryManager.CommitFiles(new TaskResultDispatcher<string>(s => { }),
                new List<string>() { "Assets\\TestDocument.txt", "foobar.txt" }, "IntegrationTest Commit", string.Empty);

            Assert.True(evt.WaitOne(5000));
            evt.Reset();

            Logger.Trace("Continue test");

            result.AssertEqual(expectedAfterCommit);

            repositoryManagerListener.Received(1).OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }

        [Test]
        public void ShouldDetectBranchChange()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            var expected = new GitStatus {
                LocalBranch = "feature/document",
                RemoteBranch = "origin/feature/document",
                Entries = new List<GitStatusEntry>()
            };

            var result = new GitStatus();
            RepositoryManager.OnRepositoryChanged += status => { result = status; };

            Logger.Trace("Issuing Command");

            RepositoryManager.SwitchBranch(new TaskResultDispatcher<string>(s => { }), "feature/document");

            Thread.Sleep(3000);

            Logger.Trace("Continue test");

            repositoryManagerListener.Received().OnRepositoryChanged(Args.GitStatus);
            result.AssertEqual(expected);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.Received(1).OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.Received(1).OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }

        [Test]
        public void ShouldDetectBranchDelete()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            Logger.Trace("Issuing Command");

            RepositoryManager.DeleteBranch(new TaskResultDispatcher<string>(s => { }), "feature/document", true);

            Thread.Sleep(2000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.Received(1).OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.Received().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }

        [Test]
        public void ShouldDetectBranchCreate()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            Logger.Trace("Issuing Command");

            RepositoryManager.CreateBranch(new TaskResultDispatcher<string>(s => { }), "feature/document2",
                "feature/document");

            Thread.Sleep(2000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.Received(1).OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);

            repositoryManagerListener.ClearReceivedCalls();

            Logger.Trace("Issuing Command");

            RepositoryManager.CreateBranch(new TaskResultDispatcher<string>(s => { }), "feature2/document2",
                "feature/document");

            Thread.Sleep(1000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.Received(1).OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);

            repositoryManagerListener.ClearReceivedCalls();
        }

        [Test]
        public void ShouldDetectChangesToRemotes()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            Logger.Trace("Issuing Command");

            RepositoryManager.RemoteRemove(new TaskResultDispatcher<string>(s => { }), "origin");

            Thread.Sleep(2000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.Received().OnActiveBranchChanged();
            repositoryManagerListener.Received().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.Received().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);

            repositoryManagerListener.ClearReceivedCalls();

            Logger.Trace("Issuing Command");

            RepositoryManager.RemoteAdd(new TaskResultDispatcher<string>(s => { }), "origin",
                "https://github.com/EvilStanleyGoldman/IOTestsRepo.git");

            Thread.Sleep(2000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.Received().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.Received().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }

        [Test]
        public void ShouldDetectGitPull()
        {
            InitializeEnvironment(TestRepoMasterCleanSynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            var expected = new GitStatus {
                LocalBranch = "master",
                RemoteBranch = "origin/master",
                Entries = new List<GitStatusEntry>()
            };

            var result = new GitStatus();
            RepositoryManager.OnRepositoryChanged += status => { result = status; };

            Logger.Trace("Issuing Command");

            RepositoryManager.Pull(new TaskResultDispatcher<string>(s => { }), "origin", "master");

            Thread.Sleep(7000);

            Logger.Trace("Continue test");

            repositoryManagerListener.Received().OnRepositoryChanged(Args.GitStatus);
            result.AssertEqual(expected);

            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            RepositoryManager.IsBusy.Should().BeFalse();

            repositoryManagerListener.Received(1).OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }

        [Test]
        public void ShouldDetectGitFetch()
        {
            InitializeEnvironment(TestRepoMasterCleanUnsynchronized);

            var repositoryManagerListener = Substitute.For<IRepositoryManagerListener>();
            repositoryManagerListener.AttachListener(RepositoryManager);

            Logger.Trace("Issuing Command");

            RepositoryManager.Fetch(new TaskResultDispatcher<string>(s => { }), "origin");

            Thread.Sleep(3000);

            Logger.Trace("Continue test");

            repositoryManagerListener.DidNotReceive().OnRepositoryChanged(Args.GitStatus);
            repositoryManagerListener.ReceivedWithAnyArgs().OnIsBusyChanged(Args.Bool);
            repositoryManagerListener.DidNotReceive().OnActiveBranchChanged();
            repositoryManagerListener.DidNotReceive().OnActiveRemoteChanged();
            repositoryManagerListener.DidNotReceive().OnHeadChanged();
            repositoryManagerListener.DidNotReceive().OnLocalBranchListChanged();
            repositoryManagerListener.Received(2).OnRemoteBranchListChanged();
            repositoryManagerListener.DidNotReceive().OnRemoteOrTrackingChanged();
            repositoryManagerListener.DidNotReceive().OnLocksUpdated(Args.EnumerableGitLock);
        }
    }
}