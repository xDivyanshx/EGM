using Xunit;
using Moq;
using EGM.Core.Services;
using EGM.Core.Interfaces;
using EGM.Core.Enums;
using EGM.Core.Entities;
using System;

namespace EGM.Core.Tests
{
    public class UpdateManagerTests
    {
        private readonly Mock<IConfigManager> _mockConfig;
        private readonly Mock<IStateManager> _mockState;
        private readonly Mock<ILogger> _mockLogger;
        private readonly Mock<IPackageValidator> _mockValidator;
        private readonly Mock<IInstallHistoryStore> _mockHistory;
        private readonly UpdateManager _updateManager;

        public UpdateManagerTests()
        {
            _mockConfig = new Mock<IConfigManager>();
            _mockState = new Mock<IStateManager>();
            _mockLogger = new Mock<ILogger>();
            _mockValidator = new Mock<IPackageValidator>();
            _mockHistory = new Mock<IInstallHistoryStore>();

            _updateManager = new UpdateManager( _mockConfig.Object, _mockState.Object, _mockLogger.Object, _mockValidator.Object, _mockHistory.Object );
        }

        [Fact]
        public void InstallPackage_SuccessPath_ShouldUpdateConfigAndHistory()
        {
            var currentVer = new Version(1, 0, 0);
            var newVer = new Version(2, 0, 0);

            _mockConfig.Setup(c => c.GetConfig()).Returns(new SystemConfig { CurrentVersion = currentVer });
            _mockState.Setup(s => s.TransitionTo(EGMStateEnum.UPDATING, It.IsAny<string>())).Returns(true);

            var expectedOutVer = newVer;
            string emptyErr;
            _mockValidator.Setup(v => v.TryValidateAndExtractVersion(It.IsAny<string>(), currentVer,out expectedOutVer,out emptyErr)).Returns(true);

            _updateManager.InstallPackage("valid_pkg_2.0.0.txt");

            _mockConfig.Verify(c => c.UpdateConfig(It.IsAny<Action<SystemConfig>>()), Times.Once);
            _mockHistory.Verify(h => h.RecordInstall(currentVer, newVer), Times.Once);
            _mockState.Verify(s => s.TransitionTo(EGMStateEnum.IDLE, "Update completed successfully"), Times.Once);
        }

        [Fact]
        public void InstallPackage_PreInstallFailure_ShouldTriggerRollback()
        {
            var currentVer = new Version(1, 0, 0);
            _mockConfig.Setup(c => c.GetConfig()).Returns(new SystemConfig { CurrentVersion = currentVer });
            _mockState.Setup(s => s.TransitionTo(EGMStateEnum.UPDATING, It.IsAny<string>())).Returns(true);

            var expectedOutVer = new Version(2, 0, 0);
            string err;
            _mockValidator.Setup(v => v.TryValidateAndExtractVersion(It.IsAny<string>(), currentVer, out expectedOutVer,out err)).Returns(true);

            _updateManager.InstallPackage("update_pkg_bad_2.0.0.txt");
            _mockHistory.Verify(h => h.RecordRollback(currentVer), Times.Once);
            _mockConfig.Verify(c => c.UpdateConfig(It.IsAny<Action<SystemConfig>>()), Times.Once);
            _mockState.Verify(s => s.TransitionTo(EGMStateEnum.IDLE, "Rollback completed"), Times.Once);
        }
    }
}