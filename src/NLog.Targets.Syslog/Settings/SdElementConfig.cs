// Licensed under the BSD license
// See the LICENSE file in the project root for more information

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets.Syslog.Extensions;

namespace NLog.Targets.Syslog.Settings
{
    /// <inheritdoc cref="NotifyPropertyChanged" />
    /// <inheritdoc cref="IDisposable" />
    /// <summary>Syslog SD-ELEMENT field configuration</summary>
    [NLogConfigurationItem]
    public class SdElementConfig : NotifyPropertyChanged, IDisposable
    {
        private SimpleLayout sdId;
        private ObservableCollection<SdParamConfig> sdParams;
        private readonly PropertyChangedEventHandler sdParamPropsChanged;
        private readonly NotifyCollectionChangedEventHandler sdParamsCollectionChanged;

        /// <summary>The SD-ID field of an SD-ELEMENT field in the STRUCTURED-DATA part</summary>
        public SimpleLayout SdId
        {
            get => sdId;
            set => SetProperty(ref sdId, value);
        }

        /// <summary>The SD-PARAM fields belonging to an SD-ELEMENT field in the STRUCTURED-DATA part</summary>
        [ArrayParameter(typeof(SdParamConfig), "SdParam")]
        public ObservableCollection<SdParamConfig> SdParams
        {
            get => sdParams;
            set => SetProperty(ref sdParams, value);
        }

        /// <summary>Builds a new instance of the SdElement class</summary>
        public SdElementConfig()
        {
            sdId = new SimpleLayout();
            sdParams = new ObservableCollection<SdParamConfig>();
            sdParamPropsChanged = (s, a) => OnPropertyChanged(nameof(SdParams));
            sdParamsCollectionChanged = CollectionChangedFactory(sdParamPropsChanged);
            sdParams.CollectionChanged += sdParamsCollectionChanged;
        }

        /// <inheritdoc />
        /// <summary>Disposes the instance</summary>
        public void Dispose()
        {
            sdParams.ForEach(x => x.PropertyChanged -= sdParamPropsChanged);
            sdParams.CollectionChanged -= sdParamsCollectionChanged;
            GC.SuppressFinalize(this);
        }
    }
}