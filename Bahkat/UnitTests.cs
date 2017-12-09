﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using Bahkat.Models;
using Bahkat.Models.AppConfigEvent;
using Bahkat.Models.PackageManager;
using Bahkat.Properties;
using Bahkat.Service;
using Bahkat.UI.Main;
using Bahkat.UI.Settings;
using Bahkat.Util;
using Microsoft.Reactive.Testing;
using Microsoft.Win32;
using Moq;
using NUnit.Framework;

namespace Windows.UI.Xaml.Controls
{
    public class UserControl
    {
    }

    public interface IPage
    {
        
    }

    public interface IPageOverrides
    {
        
    }

    public class Page : UserControl, IPage, IPageOverrides
    {
        
    }
}

namespace Bahkat
{
    public static class ObservableExtensions
    {
        public static ITestableObserver<T> Test<T>(this IObservable<T> observable, TestScheduler scheduler)
        {
            var testObserver = scheduler.CreateObserver<T>();
            observable.Subscribe(testObserver);
            return testObserver;
        }
    }

    [TestFixture]
    public class RxStoreTests
    {
        class SetValue : IStoreEvent
        {
            internal string Value;

            internal SetValue(string v)
            {
                Value = v;
            }
        }
        
        [Test]
        public void ItWorksAtAll()
        {
            var store = new RxStore<string>("hello", (state, e) =>
            {
                if (e is SetValue value)
                {
                    return value.Value;
                }
                
                return state;
            });

            var scheduler = new TestScheduler();
            var ob = store.State.Test(scheduler);

            scheduler.Start();

            Assert.AreEqual(ob.Messages.Last().Value.Value, "hello");
            
            store.Dispatch(new SetValue("test"));
            
            scheduler.AdvanceBy(1000);
            
            Assert.AreEqual(ob.Messages.Last().Value.Value, "test");
        }
    }

    [TestFixture]
    public class UpdaterTests
    {
        [SetUp]
        protected void SetUp()
        {
            
        }

        [Test]
        public void ConfigContainsDefaultValue()
        {
            var registry = new MockRegistry();
            var store = new AppConfigStore(registry);
            var scheduler = new TestScheduler();

            var x = store.State.Test(scheduler);
            scheduler.Start();
            
            Assert.AreEqual(Constants.Repository, x.Messages.Last().Value.Value.RepositoryUrl.AbsoluteUri);
        }
        
        [Test]
        public void ConfigUpdatesRepositoryUrlValue()
        {
            var registry = new MockRegistry();
            var store = new AppConfigStore(registry);
            var scheduler = new TestScheduler();

            var x = store.State.Test(scheduler);
            scheduler.Start();
            
            store.Dispatch(new SetRepositoryUrl(new Uri("https://test.example/")));
            scheduler.AdvanceBy(1000);
            
            Assert.AreEqual("https://test.example/", x.Messages.Last().Value.Value.RepositoryUrl.AbsoluteUri);
            Assert.AreEqual("https://test.example/", registry.LocalMachine
                .CreateSubKey(@"SOFTWARE\" + Constants.RegistryId)
                .Get("RepositoryUrl", ""));
        }

        [Test]
        public void UpdaterDetectsNewUpdatesForPackages()
        {
            
        }

        [Test]
        public void UpdaterShowsUIWhenUpdatesFound()
        {
            
        }

        [Test]
        public void UpdaterUsesUserPreferencesForShowingUI()
        {
            
        }
    }

    [TestFixture]
    public class UpdateWindowTests
    {
        [SetUp]
        protected void SetUp()
        {
            
        }

        [Test]
        public void ShowsPackagesToUpdate()
        {
            
        }

        [Test]
        public void CanSelectPackagesToUpdate()
        {
            
        }

        [Test]
        public void SelectedPackageShouldShowChangelog()
        {
            
        }

        [Test]
        public void PressingInstallShouldOpenMainWindowToInstallStep()
        {
            
        }

        [Test]
        public void IgnoreUpdatesShouldCloseWindow()
        {
            
        }
    }
    
    [TestFixture]
    public class MainTests
    {
        [SetUp]
        protected void SetUp()
        {
            
        }

        [Test]
        public void MainWindowCanOpenSettingsWindow()
        {
            var mock = new Mock<IMainWindowView>();
            var window = mock.Object;

            mock.Setup(view => view.OnShowSettingsClicked())
                .Returns(Observable.Return(EventArgs.Empty));

            var settingsWindowMock = new Mock<ISettingsWindowView>();

            var store = new PackageStore();
            var scheduler = new TestScheduler();
            var presenter = new MainWindowPresenter(window, store, (r) => settingsWindowMock.Object, scheduler);

            presenter.System.Test(scheduler);
            scheduler.Start();
            scheduler.AdvanceBy(1000);
            
            settingsWindowMock.Verify(v => v.Show(), Times.Once());
            
            scheduler.Stop();
        }

        [Test]
        public void MainWindowOnlyOpensOneSettingsWindow()
        {
            var mock = new Mock<IMainWindowView>();
            var window = mock.Object;

            var clickSubject = new Subject<EventArgs>();

            var settingsWindowMock = new Mock<ISettingsWindowView>();

            var store = new PackageStore();
            var scheduler = new TestScheduler();
            var presenter = new MainWindowPresenter(window, store, (r) => settingsWindowMock.Object, scheduler);
            
            mock.Setup(view => view.OnShowSettingsClicked())
                .Returns(clickSubject.AsObservable()
                .ObserveOn(scheduler)
                .SubscribeOn(scheduler));

            presenter.System.Test(scheduler);
            
            scheduler.Start();
            
            clickSubject.OnNext(EventArgs.Empty);
            clickSubject.OnNext(EventArgs.Empty);
            clickSubject.OnNext(EventArgs.Empty);
            
            scheduler.AdvanceBy(1000);
            
            settingsWindowMock.Verify(v => v.Show(), Times.Exactly(3));
            
            scheduler.Stop();
            clickSubject.Dispose();
        }

        [Test]
        public void MainWindowDefaultsToMainPage()
        {
            var mock = new Mock<IMainWindowView>();

            var store = new PackageStore();
            var scheduler = new TestScheduler();
            var presenter = new MainWindowPresenter(mock.Object, store, (r) => new Mock<ISettingsWindowView>().Object, scheduler);
            
            var x = presenter.System.Test(scheduler);
            
            scheduler.Start();
            scheduler.AdvanceBy(1000);
            
            mock.Verify(v => v.ShowPage(It.IsAny<MainPage>()), Times.Once);
        }

        [Test]
        public void MainPageAllowsSelectingPackages()
        {
            var mock = new Mock<IMainPageView>();
            var store = new PackageStore();
            var scheduler = new TestScheduler();
            var presenter = new MainPagePresenter(mock.Object,
                new RepositoryService(RepositoryApi.Create, Scheduler.CurrentThread),
                store,
                scheduler);

            mock.Setup(v => v.OnPackageSelected())
                .Returns(Observable.Return(new Package()));

            presenter.System.Test(scheduler);
            var x = store.State.Test(scheduler);
            
            scheduler.Start();
            scheduler.AdvanceBy(1000);
            
            mock.Verify(v => v.UpdateSelectedPackages(It.IsAny<IEnumerable<Package>>()), Times.Once);

            Assert.AreEqual(x.Messages.Last().Value.Value.SelectedPackages.Count, 1);
        }

        [Test]
        public void MainPageShowsPackages()
        {
            
        }

        [Test]
        public void MainPageAllowsDelectingPackages()
        {
            
        }

        [Test]
        public void MainPageAllowsInstallingSelectedPackages()
        {
            
        }

        [Test]
        public void MainPageInstallationProcessChangesPageToDownloadPage()
        {
            
        }

        [Test]
        public void PackageInstallationIsCorrectlyDetected()
        {
            
        }

        [Test]
        public void PackageUpdateStatusIsCorrectlyDetected()
        {
            
        }

        [Test]
        public void VirtualDependenciesAreProperlyDetected()
        {
            
        }

        [Test]
        public void DoubleClickingTaskbarIconWillOpenMainWindow()
        {
            
        }

        [Test]
        public void SettingsWindowShouldCloseIfMainWindowClosed()
        {
            
        }

        [Test]
        public void CanWaitForProcessToCompleteWithFailure()
        {
            var p = new ReactiveProcess(c =>
            {
                c.FileName = "ls";
                c.Arguments = "-asoidja";
            });

            var exitCode = 2;
            
            var s = new TestScheduler();
            s.Start();
            
            var x = p.Start().Test(s);
            s.AdvanceBy(1000);
            p._process.WaitForExit();
            
            Assert.AreEqual(exitCode, p._process.ExitCode);
            Assert.AreEqual(exitCode, x.Messages[0].Value.Value);
        }
        
        [Test]
        public void CanWaitForProcessToCompleteWithSuccess()
        {
            var p = new ReactiveProcess(c =>
            {
                c.FileName = "ls";
            });

            var exitCode = 0;
            
            var s = new TestScheduler();
            s.Start();
            
            var x = p.Start().Test(s);
            s.AdvanceBy(1000);
            p._process.WaitForExit();
            
            Assert.AreEqual(exitCode, p._process.ExitCode);
            Assert.AreEqual(exitCode, x.Messages[0].Value.Value);
        }
    }

    [TestFixture]
    public class MinimumViableProduct
    {
        // Opens main window to package management
        [Test]
        public void OpenMainWindowWithMainPage()
        {
            var mock = new Mock<IMainWindowView>();

            var store = new PackageStore();
            var scheduler = new TestScheduler();
            var presenter = new MainWindowPresenter(mock.Object, store, (r) => new Mock<ISettingsWindowView>().Object, scheduler);
            
            var x = presenter.System.Test(scheduler);
            
            scheduler.Start();
            scheduler.AdvanceBy(1000);
            
            mock.Verify(v => v.ShowPage(It.IsAny<MainPage>()), Times.Once);
        }

        private Repository MockRepository() => MockRepository(new Uri("http://original.example"));
        
        private Repository MockRepository(Uri uri)
        {
            var repoIndex = new RepoIndex()
            {
                Base = uri,
                Channels = new List<string> {"stable"},
                Description = new Dictionary<string, string>
                {
                    {"en", "Test Repository Description"},
                    {"sv", "Exampel Repo Text"}
                },
                Name = new Dictionary<string, string>
                {
                    {"en", "Test Repository"},
                    {"sv", "Exampel Repo"}
                },
                PrimaryFilter = "category"
            };
            
            return new Repository(repoIndex,
                new Dictionary<string, Package>(),
                new Dictionary<string, List<string>>());
        }
        
        private Func<Uri, IRepositoryApi> MockRepositoryApi =>
            uri =>
            {
                var mock = new Mock<IRepositoryApi>();
                var repo = MockRepository(uri);

                mock.Setup(x => x.RepoIndex(null))
                    .Returns(Observable.Return(repo.Meta));

                mock.Setup(x => x.PackagesIndex(null))
                    .Returns(Observable.Return(repo.PackagesIndex));

                mock.Setup(x => x.VirtualsIndex(null))
                    .Returns(Observable.Return(repo.VirtualsIndex));

                return mock.Object;
            };
        
        // Downloads the repository index
        [Test]
        public void DownloadRepositoryIndex()
        {
            var scheduler = new TestScheduler();
            var srv = new RepositoryService(MockRepositoryApi, scheduler);

            var t = srv.System.Test(scheduler);
            var testUri = new Uri("https://anything.example");
            
            scheduler.Start();
            srv.SetRepositoryUri(testUri);
            
            scheduler.AdvanceBy(1000);
            
            Assert.AreEqual(testUri, t.Messages.Last().Value.Value.Repository.Meta.Base);
        }

//        private IMainPageView MockMainPageView()
//        {
//            var mock = new Mock<IMainPageView>();
//            
//            mock.Setup(v => v.OnPackageSelected())
//                .Returns(Observable.Return(new BkPackage()));
//        }
        
        // Shows the repository information
        [Test]
        public void MainPageUpdatesOnRepositoryIndexChange()
        {
            var mock = new Mock<IMainPageView>();
            var store = new PackageStore();
            var scheduler = new TestScheduler();
            
            mock.Setup(v => v.OnPackageSelected())
                .Returns(Observable.Return(new Package()));
            mock.Setup(v => v.OnPackageDeselected())
                .Returns(Observable.Return(new Package()));
            mock.Setup(v => v.OnPrimaryButtonPressed())
                .Returns(Observable.Return(EventArgs.Empty));
            
            var repoServ = new RepositoryService(MockRepositoryApi, scheduler);
            var x = repoServ.System.Test(scheduler);
           
            var presenter = new MainPagePresenter(mock.Object, repoServ, store, scheduler);

            presenter.System.Test(scheduler);
            store.State.Test(scheduler);
            
            scheduler.Start();
            
            var testUri = new Uri("https://lol.uri.example");
            repoServ.SetRepositoryUri(testUri);
            
            scheduler.AdvanceBy(1000);

            mock.Verify(v => v.UpdatePackageList(It.IsAny<Repository>()), Times.Once);
            Assert.NotNull(x.Messages.Last().Value.Value.Repository);
        }
        
        // Will show the correct status if an installed package needs to be updated
        [Test]
        public void DemonstrateInstallationStatusHandling()
        {
            var reg = new MockRegistry();
            var pkgServ = new PackageService(reg);
            
            var v1 = new Package() {
                Version = "3.2.0.0", 
                Installer = new PackageInstaller
                {
                    InstalledSize = 1,
                    ProductCode = "test",
                    SilentArgs = "",
                    Size = 1,
                    Url = new Uri("https://lol.com")
                }
            };
            
            Assert.AreEqual(PackageInstallStatus.NotInstalled, pkgServ.GetInstallStatus(v1));
            
            var subkey = reg.LocalMachine.CreateSubKey(PackageService.UninstallKeyPath + @"\test");
            
            subkey.Set("DisplayVersion", "2.0.0.0", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.NeedsUpdate, pkgServ.GetInstallStatus(v1));
            
            subkey.Set("DisplayVersion", "2.99.1000.42", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.NeedsUpdate, pkgServ.GetInstallStatus(v1));
            
            subkey.Set("DisplayVersion", "3.0.0.0", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.NeedsUpdate, pkgServ.GetInstallStatus(v1));
            
            subkey.Set("DisplayVersion", "3.3.0.0", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.UpToDate, pkgServ.GetInstallStatus(v1));
            
            subkey.Set("DisplayVersion", "4.0.0.0", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.UpToDate, pkgServ.GetInstallStatus(v1));
            
            subkey.Set("DisplayVersion", "ahahahaha ahahahaha oh nø", RegistryValueKind.String);
            Assert.AreEqual(PackageInstallStatus.ErrorParsingVersion, pkgServ.GetInstallStatus(v1));
        }
        
        // If you select packages to be installed and uninstalled, only run the installation (for now)
//        [Test]
//        public void xxx()
//        {
//            
//        }
        

        internal class MockInstallWorker : InstallWorker
        {
            public Mock<IReactiveProcess> Mock;
            
            public MockInstallWorker(PackagePath[] packages) : base(packages)
            {
            }

            protected override IReactiveProcess CreateProcess(string path, string args)
            {
                Mock = new Mock<IReactiveProcess>();

                Mock.Setup(x => x.Output).Returns(Observable.Return(""));
                Mock.Setup(x => x.Error).Returns(Observable.Return(""));
                Mock.Setup(x => x.Start()).Returns(Observable.Return(0));

                return Mock.Object;
            }
        }

        [Test]
        public void DownloadProcessWorks()
        {
            
        }
        
        // Upon install, go to download screen
        [Test]
        public void GoToDownloadScreenWithSelectedPackagesToDownload()
        {
            
        }
        
        // Show things being downloaded
        [Test]
        public void UpdateUserInterfaceWithDownloadStatus()
        {
            
        }
        
        // Upon download, go to install screen
        [Test]
        public void GoesToInstallScreenOnceDownloadCompleted()
        {
            
        }
        
        // Install the things sequentially
        [Test]
        public void InstallPackagesSequentially()
        {
            
        }
        
        // Return to main screen
        [Test]
        public void UponInstallationCompletedPrimaryFunctionReturnsToHome()
        {
            
        }
    }
}