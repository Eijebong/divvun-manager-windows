﻿using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Input;
using Bahkat.Extensions;
using Bahkat.UI.Main;
using Bahkat.UI.Shared;

namespace Bahkat.UI.Updater
{
    /// <summary>
    /// Interaction logic for UpdateWindow.xaml
    /// </summary>
    public partial class UpdateWindow : Window, IUpdateWindowView, IDisposable
    {
        private CompositeDisposable _bag = new CompositeDisposable();
        private IObservable<PackageMenuItem> _packageToggled;
        
        public UpdateWindow()
        {
            InitializeComponent();

            _packageToggled = Observable.Merge(
                    TvPackages.ReactiveKeyDown()
                        .Where(x => x.EventArgs.Key == Key.Space)
                        .Select(_ => Unit.Default),
                    TvPackages.ReactiveDoubleClick()
                        .Select(_ => Unit.Default))
                .Select(_ => TvPackages.SelectedItem as PackageMenuItem)
                .NotNull();
            
            var app = (BahkatApp) Application.Current;
            var presenter = new UpdateWindowPresenter(this,
                app.RepositoryService,
                app.PackageService,
                app.PackageStore);
            
            _bag.Add(presenter.Start());
        }

        public IObservable<EventArgs> OnInstallClicked() => BtnPrimary.ReactiveClick().Select(x => x.EventArgs);
        public IObservable<EventArgs> OnRemindMeLaterClicked() => BtnLater.ReactiveClick().Select(x => x.EventArgs);
        public IObservable<EventArgs> OnSkipClicked() => BtnSkip.ReactiveClick().Select(x => x.EventArgs);
        public IObservable<PackageMenuItem> OnPackageToggled() => _packageToggled;

        public void StartDownloading()
        {
            var app = (BahkatApp) Application.Current;
            app.WindowService.Close<UpdateWindow>();
            app.WindowService.Show<MainWindow, DownloadPage>();
        }

        public void UpdatePrimaryButton(bool isEnabled, string label)
        {
            BtnPrimary.Content = label;
            BtnPrimary.IsEnabled = isEnabled;
        }

        public void SetPackagesModel(ObservableCollection<PackageMenuItem> items)
        {
            TvPackages.ItemsSource = items;
        }

        public void HandleError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void CloseMainWindow()
        {
            var app = (BahkatApp) Application.Current;
            app.WindowService.Close<MainWindow>();
        }

        public void Dispose()
        {
            _bag?.Dispose();
            WbView?.Dispose();
        }
    }
}
