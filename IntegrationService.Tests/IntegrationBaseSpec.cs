//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using IntegrationService.Targets;
using IntegrationService.Util;
using Kanban.API.Client.Library;
using Kanban.API.Client.Library.EventArguments;
using Kanban.API.Client.Library.TransferObjects;
using Moq;
using NUnit.Framework;
using Should;
using Ploeh.SemanticComparison.Fluent;

namespace IntegrationService.Tests
{
    public class TestTarget : TargetBase
    {
        public bool InitWasCalled = false;
        public bool CardUpdated_WasCalled = false;
        public Card UpdatedCard;
        public List<string> UpdatedItems = new List<string>();

        public TestTarget(IBoardSubscriptionManager subscriptions,
                               IConfigurationProvider<Configuration> configurationProvider,
                               ILocalStorage<AppSettings> localStorage, ILeanKitClientFactory leanKitClientFactory)
            : base(subscriptions, configurationProvider, localStorage, leanKitClientFactory)
        {
        }

        public override void Init()
        {
            InitWasCalled = true;
        }

        protected override void UpdateStateOfExternalItem(Card card, List<string> states, BoardMapping boardMapping)
        {
        }

        protected override void CardUpdated(Card card, List<string> updatedItems, BoardMapping boardMapping)
        {
            CardUpdated_WasCalled = true;
            UpdatedItems = updatedItems;
            UpdatedCard = card;
        }

		protected override void CreateNewItem(Card card, BoardMapping boardMapping) 
		{
			// do nothing
		}

        protected override void Synchronize(BoardMapping boardMapping)
        {
			// do nothing
        }

        // expose protected properties
        public new IBoardSubscriptionManager Subscriptions
        {
            get { return base.Subscriptions; }
        }

        public new Configuration Configuration
        {
            get { return base.Configuration; }
        }

        public new ILocalStorage<AppSettings> LocalStorage
        {
            get { return base.LocalStorage; }
        }

        public new ILeanKitClientFactory LeanKitClientFactory
        {
            get { return base.LeanKitClientFactory; }
        }

        public new ILeanKitApi LeanKit
        {
            get { return base.LeanKit; }
        }

        public new AppSettings AppSettings
        {
            get { return base.AppSettings; }
        }

        public void SimulateUpdateEvent(long boardId, BoardChangedEventArgs eventArgs, ILeanKitApi api)
        {
            base.BoardUpdate(boardId, eventArgs, api);
        }
    }

    public class IntegrationBaseSpec : SpecBase
    {
        protected TestTarget TestItem { get; set; }
        protected Configuration TestConfig;

        protected Mock<IBoardSubscriptionManager> MockBoardSubscriptionManager;
        protected Mock<IConfigurationProvider<Configuration>> MockConfigurationProvider;
        protected Mock<ILocalStorage<AppSettings>> MockLocalStorage;
        protected Mock<ILeanKitClientFactory> MockLeanKitClientFactory;
        protected Mock<ILeanKitApi> MockLeanKitApi;

        protected IBoardSubscriptionManager SubscriptionManager;
        protected IConfigurationProvider<Configuration> ConfigurationProvider;
        protected ILocalStorage<AppSettings> LocalStorage;
        protected ILeanKitClientFactory LeanKitClientFactory;
        protected ILeanKitApi LeanKitApi;

        protected override void OnCreateMockObjects()
        {
            MockBoardSubscriptionManager = new Mock<IBoardSubscriptionManager>();
            MockConfigurationProvider = new Mock<IConfigurationProvider<Configuration>>();
            MockLocalStorage = new Mock<ILocalStorage<AppSettings>>();
            MockLeanKitClientFactory = new Mock<ILeanKitClientFactory>();
            MockLeanKitApi = new Mock<ILeanKitApi>();

            SubscriptionManager = MockBoardSubscriptionManager.Object;
            ConfigurationProvider = MockConfigurationProvider.Object;
            LocalStorage = MockLocalStorage.Object;
            LeanKitClientFactory = MockLeanKitClientFactory.Object;
            LeanKitApi = MockLeanKitApi.Object;
        }
    }

    public class StartupSpec : IntegrationBaseSpec
    {

        protected override void OnArrange()
        {
            MockConfigurationProvider.Setup(x => x.GetConfiguration()).Returns(TestConfig);
            MockLeanKitClientFactory.Setup(x => x.Create(It.IsAny<LeanKitAccountAuth>())).Returns(LeanKitApi);
        }

        protected override void OnStartTest()
        {
            TestItem = new TestTarget(SubscriptionManager, ConfigurationProvider, LocalStorage,
                                           LeanKitClientFactory);
        }

    }

    [TestFixture]
    public class When_starting_with_a_valid_configuration : StartupSpec
    {

        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<int>())).Returns(new Board());
        }

        [Test]
        public void It_should_have_a_BoardSubscriptionManager()
        {
            TestItem.Subscriptions.ShouldNotBeNull();
        }

        [Test]
        public void It_should_have_a_Configuration()
        {
            TestItem.Configuration.AsSource().OfLikeness<Configuration>().Equals(TestConfig);
        }

        [Test]
        public void It_should_have_LocalStorage()
        {
            TestItem.LocalStorage.ShouldNotBeNull();
        }

        [Test]
        public void It_should_have_LeanKitClientFactory()
        {
            TestItem.LeanKitClientFactory.ShouldNotBeNull();
        }

        [Test]
        public void It_should_call_Init()
        {
            TestItem.InitWasCalled.ShouldBeTrue();
        }

        [Test]
        public void It_should_create_LeanKitApi()
        {
            TestItem.LeanKit.ShouldNotBeNull();
        }

        [Test]
        public void It_should_get_board_for_each_mapping()
        {
            MockLeanKitApi.Verify(x => x.GetBoard(It.IsAny<long>()), Times.Exactly(TestConfig.Mappings.Count));
        }
    }

    [TestFixture]
    public class When_starting_with_an_invalid_configuration : StartupSpec
    {

        protected override void OnArrange()
        {
            MockConfigurationProvider.Setup(x => x.GetConfiguration()).Throws<ConfigurationErrorsException>();
        }


        [Test]
        public void It_should_not_attempt_to_load_app_settings()
        {
            MockLocalStorage.Verify(x => x.Load(), Times.Never());
        }

        [Test]
        public void It_should_not_attempt_to_connect_to_leankit()
        {
            MockLeanKitClientFactory.Verify(x => x.Create(It.IsAny<LeanKitAccountAuth>()), Times.Never());
        }

        [Test]
        public void It_should_not_call_Init()
        {
            TestItem.InitWasCalled.ShouldBeFalse();	
        }
    }

    [TestFixture]
    public class When_starting_with_a_valid_RecentQueryDate : StartupSpec
    {
        protected AppSettings TestSettings;
        private DateTime _recentDate = DateTime.Now;

        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestSettings = new AppSettings {RecentQueryDate = _recentDate, BoardVersions = new Dictionary<long, long>()};
        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLocalStorage.Setup(x => x.Load()).Returns(TestSettings);
        }

        [Test]
        public void The_configuration_EarliestSyncDate_should_match_RecentQueryDate()
        {
            TestItem.Configuration.EarliestSyncDate.ShouldEqual(_recentDate);
        }
    }

    [TestFixture]
    public class When_starting_without_AppSettings : StartupSpec
    {
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
        }

        [Test]
        public void It_should_create_AppSettings_with_default_values()
        {
            TestItem.AppSettings.RecentQueryDate.ShouldEqual(TestConfig.EarliestSyncDate);
        }
    }

    [TestFixture]
    public class When_starting_with_valid_leankit_acount : StartupSpec
    {
        private LeanKitAccountAuth TestAuth;

        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestAuth = new LeanKitAccountAuth
                {
                    Hostname = TestConfig.LeanKit.Url,
					UrlTemplateOverride = "http://{0}.leankit.com",
                    Username = TestConfig.LeanKit.User,
                    Password = TestConfig.LeanKit.Password
                };
        }


        [Test]
        public void It_should_use_configured_credentials_for_leankit()
        {
            var likeness = TestAuth.AsSource().OfLikeness<LeanKitAccountAuth>();
            MockLeanKitClientFactory.Verify(x => x.Create(It.Is<LeanKitAccountAuth>(auth => likeness.Equals(auth))));
        }

    }

    [TestFixture]
    public class When_Configuration_PollingFrequency_is_omitted_or_0 : StartupSpec
    {
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestConfig.PollingFrequency = 0;
        }

        [Test]
        public void It_should_set_PollingFrequency_to_60000()
        {
            TestItem.Configuration.PollingFrequency.ShouldEqual(60000);
        }
    }

/*    [TestFixture]
    public class When_Configuration_DefaultTargetStartState_is_omitted : StartupSpec
    {
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestConfig.DefaultTargetStartState = null;
        }

        [Test]
        public void It_should_set_DefaultTargetStartState_to_Active()
        {
            TestItem.Configuration.DefaultTargetStartState.ShouldEqual("Active");
        }
    }*/

/*    [TestFixture]
    public class When_Configuration_DefaultTargetEndState_is_omitted : StartupSpec
    {
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestConfig.DefaultTargetEndState = null;
        }

        [Test]
        public void It_should_set_DefaultTargetEndState_to_Active()
        {
            TestItem.Configuration.DefaultTargetEndState.ShouldEqual("Resolved, Closed");
        }
    }*/

    [TestFixture]
    public class When_a_mapped_board_has_card_types_defined : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_configure_ValidCardTypes_to_match_board_card_types()
        {
            TestItem.Configuration.Mappings[0].ValidCardTypes
                .AsSource().OfLikeness<List<CardType>>()
                .Equals(_testBoard.CardTypes)
                .ShouldBeTrue();
        }
    }


    [TestFixture]
    public class When_no_valid_card_is_marked_as_default_and_there_is_a_card_named_task : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;
        private long _taskCardId;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            foreach (var cardType in _testBoard.CardTypes)
                cardType.IsDefault = false;
            var lastCard = _testBoard.CardTypes.Last();
            lastCard.Name = "Task";
            _taskCardId = lastCard.Id;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_mark_the_task_card_as_the_default()
        {
            var defaultCard = TestItem.Configuration.Mappings[0].ValidCardTypes.First(x => x.IsDefault);
            defaultCard.Id.ShouldEqual(_taskCardId);
        }
    }

    [TestFixture]
    public class When_no_valid_card_is_marked_as_default_and_there_is_no_card_named_task : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;
        private long _firstCardId;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            foreach (var cardType in _testBoard.CardTypes)
                cardType.IsDefault = false;

            _firstCardId = _testBoard.CardTypes.First().Id;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_mark_the_first_card_as_the_default()
        {
            var defaultCard = TestItem.Configuration.Mappings[0].ValidCardTypes.First(x => x.IsDefault);
            defaultCard.Id.ShouldEqual(_firstCardId);
        }
    }

    [TestFixture]
    public class When_a_board_has_defined_top_lovel_archive_lane : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _testBoard.ArchiveTopLevelLaneId = Test<int>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_use_the_defined_value()
        {
            TestItem.Configuration.Mappings[0].ArchiveLaneId.ShouldEqual((long) _testBoard.ArchiveTopLevelLaneId);
        }
    }

    [TestFixture]
    public class When_a_board_does_not_have_a_defined_top_level_archive_lane : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _testBoard.ArchiveTopLevelLaneId = null;
            _testBoard.Archive[0].ParentLaneId = 0;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_attempt_to_use_the_first_archive_lane_with_no_parent_lane()
        {
            var testArchiveLane = _testBoard.Archive.First(x => x.ParentLaneId == 0);
            TestItem.Configuration.Mappings[0].ArchiveLaneId.ShouldEqual((long) testArchiveLane.Id);
        }
    }

    [TestFixture]
    public class
        When_a_board_does_not_have_a_defined_top_level_archive_lane_and_there_is_no_archive_lane_without_a_parent_lane :
            StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _testBoard.ArchiveTopLevelLaneId = null;
            var archiveLane = _testBoard.AllLanes().FirstOrDefault(x => x.ClassType == LaneClassType.Archive);
            archiveLane.ParentLaneId = 0;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.ArchiveLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_attempt_to_use_the_first_archive_lane_by_cardtype_with_no_parent_lane()
        {
            var testArchiveLane =
                _testBoard.AllLanes().First(x => x.ClassType == LaneClassType.Archive && x.ParentLaneId == 0);
            TestItem.Configuration.Mappings[0].ArchiveLaneId.ShouldEqual((long) testArchiveLane.Id);
        }

    }

    [TestFixture]
    public class When_a_board_has_active_lanes : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;
        private int allLaneCount;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            allLaneCount = _testBoard.AllLanes().Count(x => x.ClassType != LaneClassType.Archive);
            // insure at least 1 active lane for testing
            if (allLaneCount == 0)
            {
                _testBoard.Lanes[0].ClassType = LaneClassType.Active;
                allLaneCount = 1;
            }
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

        }

        [Test]
        public void It_should_add_them_to_the_valid_lanes_configuration()
        {
            TestItem.Configuration.Mappings[0].ValidLanes.Count.ShouldEqual(allLaneCount);
        }

    }

    [TestFixture]
    public class When_a_board_has_active_lanes_and_no_archive_lane_set : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;
        private int allLaneCount;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            int x = 0;
            foreach (var lane in _testBoard.Lanes)
            {
                lane.Active = true;
                lane.ClassType = LaneClassType.Active;
                lane.ChildLaneIds = null;
                lane.ParentLaneId = 0;
                lane.Index = x++;
            }

            allLaneCount = _testBoard.AllLanes().Count(y => y.ClassType != LaneClassType.Archive);
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.ArchiveLaneId = 0;
            _testBoard.ArchiveTopLevelLaneId = null;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_add_them_to_the_valid_lanes_configuration()
        {
            TestItem.Configuration.Mappings[0].ValidLanes.Count.ShouldEqual(allLaneCount);
        }

        [Test]
        public void The_first_lane_by_index_should_be_marked_IsFirst()
        {
            var firstLaneByIndex = _testBoard.Lanes.First(x => x.Index == 0);
            _mapping.ValidLanes.First(x => x.IsFirst).Id.ShouldEqual((long) firstLaneByIndex.Id);
        }

        [Test]
        public void The_last_lane_by_index_should_be_marked_IsLast()
        {
            var maxIndex = _testBoard.AllLanes().Max(x => x.Index);
            var lastLaneByIndex = _testBoard.AllLanes().First(x => x.Index == maxIndex);
            _mapping.ValidLanes.First(x => x.IsLast).Id.ShouldEqual((long) lastLaneByIndex.Id);
        }

        [Test]
        public void All_other_lanes_should_not_be_marked()
        {
            var firstNorLast = _mapping.ValidLanes.Where(x => !x.IsFirst && !x.IsLast);
            firstNorLast.Count().ShouldEqual(allLaneCount - 2);
        }
    }

    [TestFixture]
    public class When_a_board_has_active_lanes_and_has_archive_lanes : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;
        private int activeLaneCount;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            int x = 0;
            foreach (var lane in _testBoard.Lanes)
            {
                lane.Active = true;
                lane.ClassType = LaneClassType.Active;
                lane.Index = x++;
            }

            // assign an arbitrary archive lane as the top level archive
            var lastArchiveItem = _testBoard.Archive.Last();
            lastArchiveItem.ParentLaneId = 0;
            _testBoard.ArchiveTopLevelLaneId = lastArchiveItem.Id;

            activeLaneCount = _testBoard.Lanes.Count;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_add_archive_lanes_to_the_valid_lanes_configuration()
        {
            _mapping.ValidLanes.FirstOrDefault(x => x.Id == _mapping.ArchiveLaneId).ShouldNotBeNull();
        }

        [Test]
        public void The_archive_lane_should_be_marked_IsLast()
        {
            _mapping.ValidLanes.FirstOrDefault(x => x.IsLast).Id.ShouldEqual(_mapping.ArchiveLaneId);
        }

    }

 /*   [TestFixture]
    public class When_start_item_is_not_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start = null;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_create_a_default_value()
        {
            _mapping.Start.ShouldNotBeNull();
        }
    }*/

 /*   [TestFixture]
    public class When_start_state_is_not_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.State = null;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_use_the_defaultTargetStartState()
        {
            _mapping.Start.State.ShouldEqual(TestItem.Configuration.DefaultTargetStartState);
        }
    }*/

/*    [TestFixture]
    public class When_start_lane_and_lanename_are_not_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.Lane = 0;
            _mapping.Start.LaneName = null;
            _testBoard.Lanes[0].Title = _mapping.Start.State;
            _testBoard.Lanes[0].ChildLaneIds = null;
            _testBoard.Lanes[0].ParentLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_use_the_first_lane_matching_the_start_state_value()
        {
            _mapping.Start.Lane.ShouldEqual(
                (long) _testBoard.Lanes.FirstOrDefault(x => x.Title == _mapping.Start.State).Id);
        }
    }*/

/*    [TestFixture]
    public class When_start_lane_is_not_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.Lane = 0;
            _testBoard.Lanes[0].Title = _mapping.Start.LaneName;
            _testBoard.Lanes[0].ChildLaneIds = null;
            _testBoard.Lanes[0].ParentLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_use_the_first_lane_matching_the_start_laneName_value()
        {
            _mapping.Start.Lane.ShouldEqual(
                (long) _testBoard.Lanes.FirstOrDefault(x => x.Title == _mapping.Start.LaneName).Id);
        }
    }*/

/*    [TestFixture]
    public class When_valid_start_lane_is_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.Lane = (long) _testBoard.Lanes[0].Id;
            _testBoard.Lanes[0].ChildLaneIds = null;
            _testBoard.Lanes[0].ParentLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};

        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_assign_the_laneName_to_match_the_valid_lane()
        {
            _mapping.Start.LaneName.ShouldEqual(
                _testBoard.Lanes.FirstOrDefault(x => x.Id == _mapping.Start.Lane).Title);
        }
    }*/

/*    [TestFixture]
    public class When_invalid_start_lane_and_no_laneName_is_specified_in_a_mapping : StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.Lane = 100000;
            _mapping.Start.LaneName = null;
            _testBoard.Lanes[0].Title = _mapping.Start.State;
            _testBoard.Lanes[0].ChildLaneIds = null;
            _testBoard.Lanes[0].ParentLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};
        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_use_the_first_lane_matching_the_start_state_value()
        {
            _mapping.Start.Lane.ShouldEqual(
                (long) _testBoard.Lanes.FirstOrDefault(x => x.Title == _mapping.Start.State).Id);
        }
    }*/

/*    [TestFixture]
    public class When_start_lane_is_not_specified_in_a_mapping_and_the_state_does_not_match_a_valid_lane :
        StartupSpec
    {
        private Board _testBoard;
        private BoardMapping _mapping;

        protected override void OnStartFixture()
        {
            _testBoard = Test<Board>.Item;
            _testBoard.Lanes[0].Index = 0;
            _mapping = Test<BoardMapping>.Item;
            _mapping.Identity.LeanKit = _testBoard.Id;
            _mapping.Start.Lane = 0;
            _mapping.ValidLanes[0].IsFirst = true;
            _testBoard.Lanes[0].ChildLaneIds = null;
            _testBoard.Lanes[0].ParentLaneId = 0;
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {_mapping};
        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
        }

        [Test]
        public void It_should_use_the_first_valid_lane()
        {
            _mapping.Start.Lane.ShouldEqual(_mapping.ValidLanes.FirstOrDefault(x => x.IsFirst).Id);
        }
    }*/

/*	[TestFixture]
	public class When_start_lane_is_specified_in_a_mapping_and_the_specified_start_lane_contains_child_lanes : StartupSpec
	{
		private Board _testBoard;
		private BoardMapping _mapping;
		private Kanban.API.Client.Library.TransferObjects.Lane _childLane1;
		private Kanban.API.Client.Library.TransferObjects.Lane _childLane2;
		private Kanban.API.Client.Library.TransferObjects.Lane _childLane3;

		protected override void OnStartFixture()
		{
			_testBoard = Test<Board>.Item;
			_testBoard.Lanes[0].Index = 0;
			var parentLane = _testBoard.Lanes[0];
			parentLane.ParentLaneId = 0;

			// Create and configure child lanes
			_childLane1 = Test<Kanban.API.Client.Library.TransferObjects.Lane>.Item;
			_childLane2 = Test<Kanban.API.Client.Library.TransferObjects.Lane>.Item;
			_childLane3 = Test<Kanban.API.Client.Library.TransferObjects.Lane>.Item;

			_childLane1.ChildLaneIds = null;
			_childLane1.ParentLaneId = parentLane.Id.Value;
			_childLane1.SiblingLaneIds = new List<long> {_childLane2.Id.Value, _childLane3.Id.Value};
			_childLane1.Index = 0;

			_childLane2.ChildLaneIds = null;
			_childLane2.ParentLaneId = parentLane.Id.Value;
			_childLane2.SiblingLaneIds = new List<long> { _childLane1.Id.Value, _childLane3.Id.Value };
			_childLane3.ChildLaneIds = null;
			_childLane3.ParentLaneId = parentLane.Id.Value;
			_childLane3.SiblingLaneIds = new List<long> { _childLane1.Id.Value, _childLane2.Id.Value };

			_testBoard.Lanes.Add(_childLane1);
			_testBoard.Lanes.Add(_childLane2);
			_testBoard.Lanes.Add(_childLane3);

			// Set child lane ids on the parent lane
			parentLane.ChildLaneIds = new List<long> { _childLane1.Id.Value, _childLane2.Id.Value, _childLane3.Id.Value };

			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.Start.Lane = parentLane.Id.Value;
			_mapping.ValidLanes[0].IsFirst = true;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange()
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_use_the_first_valid_child_lane()
		{
			_mapping.Start.Lane.ShouldEqual(_childLane1.Id.GetValueOrDefault());
			_mapping.Start.LaneName.ShouldEqual(_childLane1.Title);
		}
	}*/

/*	[TestFixture]
	public class When_intermediatelanes_are_not_specified : StartupSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.Start = null;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_have_no_intermediate_lanes() 
		{
			_mapping.LaneToStatesMap.Count.ShouldEqual(0);
		}
	}*/

/*	[TestFixture]
	public class When_valid_intermediate_lane_is_specified_in_a_mapping : StartupSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
        {
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add((long)_testBoard.Lanes[0].Id, new List<string>() { "bob" });
			_testBoard.Lanes[0].ChildLaneIds = null;
			_testBoard.Lanes[0].ParentLaneId = 0;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };

		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_assign_the_laneName_to_match_the_valid_lane() 
		{
			_mapping.LanesStateMaps[0].LaneName.ShouldEqual(_testBoard.Lanes.FirstOrDefault(x => x.Id == _mapping.LanesStateMaps[0].Lane).Title);
		}
	}*/


/*	[TestFixture]
	public class When_invalid_lane_is_specified_in_an_intermediatelane_mapping : StartupSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add(100000, new List<string>() { "Bob"});
			_testBoard.Lanes[0].Title = _mapping.LaneToStatesMap[0][0];
			_testBoard.Lanes[0].ChildLaneIds = null;
			_testBoard.Lanes[0].ParentLaneId = 0;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_have_not_create_an_intermediate_lane() 
		{
			_mapping.LaneToStatesMap.Count.ShouldEqual(0);
		}
	}*/

/*	[TestFixture]
	public class When_no_state_is_specified_in_an_intermediatelane_mapping : StartupSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() {
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add((long)_testBoard.Lanes[0].Id, new List<string>());
			_testBoard.Lanes[0].ChildLaneIds = null;
			_testBoard.Lanes[0].ParentLaneId = 0;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };

		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_not_create_an_intermediate_lane() 
		{
			_mapping.LaneToStatesMap.Count.ShouldEqual(0);
		}
	}*/

    public class BoardChangeSpec : IntegrationBaseSpec
    {
        protected CloneableCard TestCard;
        protected string NewValue = Test<String>.Item;
        protected int BoardId = Test<int>.Item;
        protected CardUpdateEvent CardUpdateEvent;
        protected override void OnArrange()
        {
            TestConfig = Test<Configuration>.Item;
            TestConfig.Mappings = new List<BoardMapping> {TestConfig.Mappings[0]}; 
            MockConfigurationProvider.Setup(x => x.GetConfiguration()).Returns(TestConfig);
            MockLeanKitClientFactory.Setup(x => x.Create(It.IsAny<LeanKitAccountAuth>())).Returns(LeanKitApi);
            var testSettings = Test<AppSettings>.Item;
            MockLocalStorage.Setup(x => x.Load()).Returns(testSettings);
            TestItem = new TestTarget(SubscriptionManager, ConfigurationProvider, LocalStorage,
                                           LeanKitClientFactory);
            TestCard = Test<CloneableCard>.Item;
            TestConfig.Mappings[0].Identity.LeanKit = BoardId;
        }

        protected override void OnStartTest()
        {
            var api = LeanKitClientFactory.Create(new LeanKitAccountAuth());
            var eventArgs = new BoardChangedEventArgs
                {
                    UpdatedCards = new List<CardUpdateEvent> { CardUpdateEvent }
                };
            TestItem.SimulateUpdateEvent(BoardId, eventArgs, api);
        }
    }

    [TestFixture]
    public class When_a_card_title_is_changed_and_sync_title_is_disabled : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Title = NewValue;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = false);
        }
        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }
    [TestFixture]
    public class When_a_card_title_is_changed_but_there_is_no_external_id : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Title = NewValue;
            CardUpdateEvent.UpdatedCard.ExternalCardID = null;
            TestConfig.Mappings.ForEach(x => x.UpdateCards = true);
        }
        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class When_a_card_title_is_changed_and_sync_title_is_enabled : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Title = NewValue;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = true);
        }
        [Test]
        public void It_should_notify_target_system_by_via_CardUpdated_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeTrue();
        }

        [Test]
        public void It_should_provide_the_updated_card()
        {
            TestItem.UpdatedCard.Id.ShouldEqual(TestCard.Id);
        }

        [Test]
        public void It_should_specify_the_Title_was_changed()
        {
         TestItem.UpdatedItems.ShouldContain("Title");   
        }

        [Test]
        public void It_should_have_the_new_title()
        {
            TestItem.UpdatedCard.Title.ShouldEqual(NewValue);
        }
    }

    [TestFixture]
    public class When_a_card_description_is_changed_and_sync_description_is_disabled : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Description = NewValue;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = false);
        }

        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class When_a_card_description_is_changed_but_there_is_no_external_id : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Description = NewValue;
            CardUpdateEvent.UpdatedCard.ExternalCardID = null;
            TestConfig.Mappings.ForEach(x => x.UpdateCards = true);
        }
        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class When_a_card_description_is_changed_and_sync_description_is_enabled : BoardChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Description = NewValue;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = true);
        }
        [Test]
        public void It_should_notify_target_system_by_via_CardUpdated_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeTrue();
        }

        [Test]
        public void It_should_provide_the_updated_card()
        {
            TestItem.UpdatedCard.Id.ShouldEqual(TestCard.Id);
        }

        [Test]
        public void It_should_specify_the_Description_was_changed()
        {
			TestItem.UpdatedItems.ShouldContain("Description");   
        }

        [Test]
        public void It_should_have_the_new_description()
        {
            TestItem.UpdatedCard.Description.ShouldEqual(NewValue);
        }
    }

    public class PriorityChangeSpec:BoardChangeSpec
    {
        public int NewPriority = Test<int>.Item;
    }

    [TestFixture]
    public class When_a_card_priority_is_changed_and_sync_priority_is_disabled : PriorityChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Priority = NewPriority;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = false);
        }

        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class When_a_card_priority_is_changed_but_there_is_no_external_id : PriorityChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Priority = NewPriority;
            CardUpdateEvent.UpdatedCard.ExternalCardID = null;
            TestConfig.Mappings.ForEach(x => x.UpdateCards = true);
        }
        [Test]
        public void It_should_not_send_an_update_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeFalse();
        }
    }

    [TestFixture]
    public class When_a_card_priority_is_changed_and_sync_priority_is_enabled : PriorityChangeSpec
    {
        protected override void OnArrange()
        {
            base.OnArrange();
            CardUpdateEvent = new CardUpdateEvent(DateTime.Now, TestCard, TestCard.Clone());
            CardUpdateEvent.UpdatedCard.Priority = NewPriority;
            TestConfig.Mappings.ForEach(x => x.UpdateTargetItems = true);
        }
        [Test]
        public void It_should_notify_target_system_by_via_CardUpdated_event()
        {
            TestItem.CardUpdated_WasCalled.ShouldBeTrue();
        }

        [Test]
        public void It_should_provide_the_updated_card()
        {
            TestItem.UpdatedCard.Id.ShouldEqual(TestCard.Id);
        }

        [Test]
        public void It_should_specify_the_Priority_was_changed()
        {
         TestItem.UpdatedItems.ShouldContain("Priority");   
        }

        [Test]
        public void It_should_have_the_new_priority()
        {
            TestItem.UpdatedCard.Priority.ShouldEqual(NewPriority);
        }
    }

    public class CloneableCard:Card
    {
        public CloneableCard Clone()
        {
            return (CloneableCard)MemberwiseClone();
        }
    }

}
