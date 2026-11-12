// Stand-ins for the distributed key search (CrypCloud / VoluntLib2) of the KeySearcher in the browser.
//
// The browser build has no peer-to-peer network, so the KeySearcher always searches locally. These types replace
// the parts of CrypCloudCore and VoluntLib2 the KeySearcher sources refer to, with the behaviour of "CrypCloud is
// not running": no jobs, no workers. tools-gen-plugins.py compiles this file instead of the cloud sources
// (see SOURCE_SUBSTITUTES there).
using System;
using System.Collections.Generic;
using System.Numerics;
using CrypTool.PluginBase;

namespace VoluntLib2.Tools
{
    /// <summary>Binary serialization of job results (KeyResultEntry implements it).</summary>
    public interface IVoluntLibSerializable
    {
        byte[] Serialize();

        void Deserialize(byte[] bytes);
    }

    /// <summary>Progress of a distributed job (the P2P view model handles it; never raised in the browser).</summary>
    public class JobProgressEventArgs : EventArgs
    {
        public BigInteger JobId { get; set; }

        public List<byte[]> ResultList { get; set; }

        public BigInteger NumberOfBlocks { get; set; }

        public BigInteger NumberOfCalculatedBlocks { get; set; }
    }

    public enum TaskEventArgType
    {
        Started,
        Finished,
        Canceled,
        Progress
    }

    /// <summary>A block of a distributed job started, ended or progressed (never raised in the browser).</summary>
    public class TaskEventArgs : EventArgs
    {
        public int TaskProgress { get; set; }

        public BigInteger BlockID { get; set; }

        public TaskEventArgType Type { get; set; }

        public BigInteger JobId { get; set; }
    }
}

namespace VoluntLib2.ManagementLayer
{
    /// <summary>A distributed job; never exists in the browser.</summary>
    public class Job
    {
        public string JobName { get; set; }

        public BigInteger NumberOfBlocks { get; set; }
    }
}

namespace CrypCloud.Core
{
    /// <summary>The CrypCloud client, always offline in the browser.</summary>
    public class CrypCloudCore
    {
        public static CrypCloudCore Instance { get; } = new CrypCloudCore();

        public bool IsRunning => false;

        public bool WritePerformanceLog => false;

        public int AmountOfWorker => 1;

        public VoluntLib2.ManagementLayer.Job GetJobById(BigInteger jobId) => null;

        public BigInteger GetCalculatedBlocksOfJob(BigInteger jobId) => BigInteger.Zero;
    }
}

namespace KeySearcher.CrypCloud
{
    using global::KeySearcherPresentation.Controls;

    /// <summary>
    /// Replaces the cloud key search: starting it only reports that the distributed search is not available.
    /// The KeySearcher setting "use peer-to-peer" is the only way to get here.
    /// </summary>
    internal class CloudKeySearcher
    {
        private readonly KeySearcher _keySearcher;

        public CloudKeySearcher(JobDataContainer jobDataContainer, KeyPattern.KeyPattern pattern, P2PQuickWatchPresentation presentation, KeySearcher keySearcher)
        {
            _keySearcher = keySearcher;
        }

        public void Start()
        {
            _keySearcher.GuiLogMessage("The distributed key search (CrypCloud) is not available in the browser. Switch off peer-to-peer to search locally.", NotificationLevel.Error);
            _keySearcher.IsKeySearcherRunning = false;
        }

        public void Stop() { }
    }
}
