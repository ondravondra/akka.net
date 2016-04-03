using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Query.Dsl;

namespace Akka.Persistence.Query
{
    public class PersistenceQuery : IExtension
    {
        private readonly Config _config;
        private readonly ExtendedActorSystem _system;
        private readonly ILoggingAdapter _log;

        public PersistenceQuery(ExtendedActorSystem system)
        {
            _system = system;
            _config = system.Settings.Config.GetConfig("akka.persistence");
            _log = Logging.GetLogger(_system, this);
        }

        public IActorRef ReadJournalFor(string readJournalPluginId)
        {
            return null;
        }

        private IReadJournal CreatePlugin(string configPath)
        {
            if (string.IsNullOrEmpty(configPath))
                throw new ArgumentNullException(nameof(configPath), $"'reference.conf' is missing persistence read journal plugin config path: '${configPath}'");

            var pluginConfig = _system.Settings.Config.GetConfig(configPath);
            var pluginClassName = pluginConfig.GetString("class");
            var pluginType = Type.GetType(pluginClassName, true);

            return null;
        }
    }
}
