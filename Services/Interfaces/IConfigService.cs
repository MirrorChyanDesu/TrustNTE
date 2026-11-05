using System;
using TrustNTE.Models;

namespace TrustNTE.Services.Interfaces
{
    public interface IConfigService
    {
        AppConfig Load();
        void Save(AppConfig config);
        void Update(Action<AppConfig> mutateAction);
    }
}