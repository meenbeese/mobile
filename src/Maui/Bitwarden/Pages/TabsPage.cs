﻿using System;
using System.Threading.Tasks;
using Bit.App.Effects;
using Bit.App.Models;
using Bit.App.Resources;
using Bit.Core;
using Bit.Core.Abstractions;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;
using Microsoft.Maui.Controls;
using Microsoft.Maui;

namespace Bit.App.Pages
{
    public class TabsPage : TabbedPage
    {
        private readonly IBroadcasterService _broadcasterService;
        private readonly IMessagingService _messagingService;
        private readonly IKeyConnectorService _keyConnectorService;
        private readonly IStateService _stateService;
        private readonly LazyResolve<ILogger> _logger = new LazyResolve<ILogger>("logger");

        private NavigationPage _groupingsPage;
        private NavigationPage _sendGroupingsPage;
        private NavigationPage _generatorPage;

        public TabsPage(AppOptions appOptions = null, PreviousPageInfo previousPage = null)
        {
            _broadcasterService = ServiceContainer.Resolve<IBroadcasterService>("broadcasterService");
            _messagingService = ServiceContainer.Resolve<IMessagingService>("messagingService");
            _keyConnectorService = ServiceContainer.Resolve<IKeyConnectorService>("keyConnectorService");
            _stateService = ServiceContainer.Resolve<IStateService>();

            _groupingsPage = new NavigationPage(new GroupingsPage(true, previousPage: previousPage))
            {
                Title = AppResources.MyVault,
                IconImageSource = "lock.png"
            };
            Children.Add(_groupingsPage);

            _sendGroupingsPage = new NavigationPage(new SendGroupingsPage(true, null, null, appOptions))
            {
                Title = AppResources.Send,
                IconImageSource = "send.png",
            };
            Children.Add(_sendGroupingsPage);

            _generatorPage = new NavigationPage(new GeneratorPage(true, null, this))
            {
                Title = AppResources.Generator,
                IconImageSource = "generate.png"
            };
            Children.Add(_generatorPage);

            var settingsPage = new NavigationPage(new SettingsPage(this))
            {
                Title = AppResources.Settings,
                IconImageSource = "cog_settings.png"
            };
            Children.Add(settingsPage);

            // TODO Xamarin.Forms.Device.RuntimePlatform is no longer supported. Use Microsoft.Maui.Devices.DeviceInfo.Platform instead. For more details see https://learn.microsoft.com/en-us/dotnet/maui/migration/forms-projects#device-changes
            if (Device.RuntimePlatform == Device.Android)
            {
                Effects.Add(new TabBarEffect());

                Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetToolbarPlacement(this,
                    Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.ToolbarPlacement.Bottom);
                Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetIsSwipePagingEnabled(this, false);
                Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.TabbedPage.SetIsSmoothScrollEnabled(this, false);
            }

            if (appOptions?.GeneratorTile ?? false)
            {
                appOptions.GeneratorTile = false;
                ResetToGeneratorPage();
            }
            else if (appOptions?.MyVaultTile ?? false)
            {
                appOptions.MyVaultTile = false;
            }
            else if (appOptions?.CreateSend != null)
            {
                ResetToSendPage();
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            _broadcasterService.Subscribe(nameof(TabsPage), async (message) =>
            {
                if (message.Command == "syncCompleted")
                {
                    Device.BeginInvokeOnMainThread(async () => await UpdateVaultButtonTitleAsync());
                }
            });
            await UpdateVaultButtonTitleAsync();
            if (await _keyConnectorService.UserNeedsMigration())
            {
                _messagingService.Send("convertAccountToKeyConnector");
            }

            var forcePasswordResetReason = await _stateService.GetForcePasswordResetReasonAsync();

            if (forcePasswordResetReason.HasValue)
            {
                _messagingService.Send(Constants.ForceUpdatePassword);
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _broadcasterService.Unsubscribe(nameof(TabsPage));
        }

        public void ResetToVaultPage()
        {
            CurrentPage = _groupingsPage;
        }

        public void ResetToGeneratorPage()
        {
            CurrentPage = _generatorPage;
        }

        public void ResetToSendPage()
        {
            CurrentPage = _sendGroupingsPage;
        }

        protected async override void OnCurrentPageChanged()
        {
            if (CurrentPage is NavigationPage navPage)
            {
                if (_groupingsPage?.RootPage is GroupingsPage groupingsPage)
                {
                    await groupingsPage.HideAccountSwitchingOverlayAsync();
                }

                _messagingService.Send("updatedTheme");
                if (navPage.RootPage is GroupingsPage)
                {
                    // Load something?
                }
                else if (navPage.RootPage is GeneratorPage genPage)
                {
                    await genPage.InitAsync();
                }
                else if (navPage.RootPage is SettingsPage settingsPage)
                {
                    await settingsPage.InitAsync();
                }
            }
        }

        public void OnPageReselected()
        {
            if (_groupingsPage?.RootPage is GroupingsPage groupingsPage)
            {
                groupingsPage.HideAccountSwitchingOverlayAsync().FireAndForget();
            }
        }

        private async Task UpdateVaultButtonTitleAsync()
        {
            try
            {
                var policyService = ServiceContainer.Resolve<IPolicyService>("policyService");
                var isShowingVaultFilter = await policyService.ShouldShowVaultFilterAsync();
                _groupingsPage.Title = isShowingVaultFilter ? AppResources.Vaults : AppResources.MyVault;
            }
            catch (Exception ex)
            {
                _logger.Value.Exception(ex);
            }
        }
    }
}