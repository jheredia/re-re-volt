using NUnit.Framework;
using ReReVolt.Online.Unity;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

namespace ReReVolt.Tests.Online
{
    public sealed class OnlineLocalInputSenderTests : InputTestFixture
    {
        [Test]
        public void CapacityCountsOnlyUnpairedDevicesAndStopsAtFourRacers()
        {
            Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.Zero);
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad firstPad = InputSystem.AddDevice<Gamepad>();
            Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.EqualTo(2));

            InputUser keyboardOwner = InputUser.PerformPairingWithDevice(keyboard);
            InputUser padOwner = InputUser.PerformPairingWithDevice(firstPad);
            try
            {
                Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.Zero,
                    "Devices already owned by another local input user cannot reserve online slots.");
                padOwner.UnpairDevicesAndRemoveUser();
                Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.EqualTo(1));

                InputSystem.DisableDevice(firstPad);
                Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.Zero);
                InputSystem.EnableDevice(firstPad);
                Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.EqualTo(1));

                for (int i = 0; i < 4; i++) InputSystem.AddDevice<Gamepad>();
                Assert.That(OnlineLocalInputSender.AvailableRacerCapacity(), Is.EqualTo(4),
                    "The lobby must not advertise more than the four-racer grid supports.");
            }
            finally
            {
                if (padOwner.valid) padOwner.UnpairDevicesAndRemoveUser();
                if (keyboardOwner.valid) keyboardOwner.UnpairDevicesAndRemoveUser();
            }
        }
    }
}
