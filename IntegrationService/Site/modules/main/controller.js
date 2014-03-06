/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _, niceTools) {


    Main.controllers.ConfigurationController = Marionette.Controller.extend({
        initialize: function () {
            this.wizardMode = false; // when true, user got to this page by pressing the 'next' button

            this.loadConfigurationModel();

            this.pages = { connectToLeanKit: undefined, connectToTarget: undefined, settings: undefined, boardConfiguration: undefined, activate: undefined };
            this.pageNav = { connectToLeanKit: "Connect to LeanKit", connectToTarget: "Connect to Target", settings: "Settings", boardConfiguration: "Board Configuration", activate: "Activate..." };
            this.pageKeys = _.keys(this.pages);
            this.current = { index: -1, view: null, pageName: "" };

            App.reqres.setHandler('getTargetType', this.getTargetType, this);
            App.reqres.setHandler('targetAllowsIterationPath', this.targetAllowsIterationPath, this);
            App.reqres.setHandler('targetAllowsCustomQuery', this.targetAllowsCustomQuery, this);
            
            this.view = new Main.views.PageLayoutView({ controller: this });

        },

        loadConfigurationModel: function () {
            this.configuration = new Main.models.Configuration();
            var loading = this.configuration.fetch({ context: this });
            $.when(loading).done(this.loadLaneNames);
        },

        loadLaneNames: function (model) {
            var mappings = this.configuration.Mappings();

            if (mappings.length === 0) {
                this.laneNames = {};
                App.Config.loadPages();
                App.Config.selectDefaultPage();
                return;
            }
            
            var parms = $.param(this.configuration.LeanKit().attributes);
            mappings.each(function (mapping) {
                parms += "&boardIds=" + mapping.get("BoardId");
            });

            $.ajax({
                url: "/lanenames",
                data: parms,
                context: this,
                contentType: "application/json",
                success: function(result) {
                    this.laneNames = result;
                    if (_.isObject(model)) {
                        App.Config.loadPages();
                        App.Config.selectDefaultPage();
                    }
                },
                error: function () {
                    alert("Load Board Lane Names failed, please verify your settings on the 'Connect to LeanKit' tab.");
                }
            });
        },

        viewIsShown: function () {
            this.createNavigation();
        },
        
        createNavigation:function () {
            var navbarView = new Main.views.NavBarView();
            this.listenTo(navbarView, 'page:activated', this.pageActivated, this);
            this.listenTo(navbarView, 'page:deactivated', this.pageDeactivated, this);
            this.view.nav.show(navbarView);
            navbarView.displayPageNav(this.pageNav);
            this.navbarView = navbarView;
        },
        
        loadPages:function() {
            // populate pages container
            var page;
            _.each(this.pageKeys, function(key) {
                page = this.getPage(key);
                if(page.isValid()) {
                    this.pageValidated(key);
                }
            }, this);
        

        },

        selectDefaultPage:function () {
            // display the first invalid page, or the board config page if all are valid
            var page;
            for (var i = 0; i < this.pageKeys.length; i++) {
                page = this.pages[this.pageKeys[i]]
                if(!page.isValid()) {
                    this.displayPage(page, i);
                    this.navbarView.selectPage(page.pageName, true, true);
                    return;
                }
            }
            // no page was invalid; show the board config page
            this.gotoPage('boardConfiguration');
            this.navbarView.selectPage("boardConfiguration", true, true);
        },
        
        gotoPage: function (pageId) {
            if (_.isObject(this.current.page)) {
                this.current.page.triggerMethod('page:leave');
                this.stopListening(this.current.page);
            }
            var page, index;
            if (pageId === "next") {
                pageId = ++this.current.index;
                if (pageId < this.pageKeys.length) {
                    return this.gotoPage(pageId);
                }
            }
            if (pageId === "prev") {
                pageId = --this.current.index;
                if (pageId >= 0) {
                    return this.gotoPage(pageId);

                }
            }

            if (_.isNumber(pageId)) {
                page = this.getPage(this.pageKeys[pageId]);
                index = pageId;
            } else if (_.isString(pageId)) {
                page = this.getPage(pageId);
                index = this.pageKeys.indexOf(pageId);
            }
            
            this.displayPage(page, index);
        },

        displayPage: function (page, index) {
            if (!_.isObject(page)) return;

            page.triggerMethod("page:show");

            if (!_.isObject(page.view))
                App.log('Error: page view [' + page.pageName + '] not available');
            else {
                this.view.showPage(page.view);
                this.current = { index: index, page: page, pageName: page.pageName };
                this.listenTo(page, "pageValidated", this.pageValidated, this);
            }

        },
        
        getNextPageId: function () {
            var index;
            index = ++this.current.index;
            if (index < this.pageKeys.length) {
                return this.pageKeys[index];
            } else {
                return undefined;
            }
        },

        getPrevPageId: function () {
            var index = --this.current.index;
            if (index >= 0) {
                return this.pageKeys[index];
            } else {
                return undefined;
            }
        },

        getPage: function (pageName) {
            if (_.isObject(this.pages[pageName]))
                return this.pages[pageName];

            var pageController;
            switch (pageName) {
                case "connectToLeanKit":
                    pageController = new Main.controllers.LeanKitLoginController({ owner: this, pageName: pageName, model: this.configuration.get("LeanKit") });
                    break;
                case "connectToTarget":
                    pageController = new Main.controllers.TargetLoginController({ owner: this, pageName: pageName, model: this.configuration.get("Target") });
                    break;
                case "boardConfiguration":
                    Main.boardConfiguration = new Main.controllers.BoardConfigurationController({ owner: this, pageName: pageName, model: this.configuration.Mappings(), credentials: { leankit: this.configuration.get("LeanKit"), target: this.configuration.get("Target") } });
                    pageController = Main.boardConfiguration;
                    break;
                case "settings":
                    Main.settings = new Main.controllers.SettingsController({ owner: this, pageName: pageName, model: this.configuration.Settings() });
                    pageController = Main.settings;
                    break;
                case "activate":
                    Main.activate = new Main.controllers.ActivateController({ owner: this, pageName: pageName, model:this.configuration });
                    pageController = Main.activate;
                    break;
                default:
                    return null;
            }
            // stash
            this.pages[pageName] = pageController;
            
            return pageController;
        },

        pageActivated: function (pageId) {
            this.view.triggerMethod('hide:next');
            this.gotoPage(pageId);
        },

        pageDeactivated: function (pageId) {

        },

        pageValidated: function (pageId, m, nextMsg) {
            // show/hide next button
            if (nextMsg)
                this.view.triggerMethod('show:next', nextMsg);
            else
                this.view.triggerMethod('hide:next');
            // mark the page as validated
            this.navbarView.markPage(pageId);
        },
        
        nextPageRequested: function () {
            this.wizardMode = true;
            var pageId = this.getNextPageId();
            if (_.isString(pageId))
                this.navbarView.selectPage(pageId, true);
            this.wizardMode = false;
        },
        
        // available at: App.Config.configuration.attributes.Target.attributes
        getTargetType:function () {
            if(_.isObject(this.configuration))
            {
                return this.configuration.Target().Type();
            }
            return "Target";
        },
        
        targetAllowsIterationPath: function () {
            return this.getTargetType().toLowerCase() === "tfs";
        },
        
        targetAllowsCustomQuery: function() {
            var targetType = this.getTargetType().toLowerCase();
            if (targetType == "tfs")
                return true;
            if (targetType == "jira")
                return true;
            return false;
        },

        validatePages: function () {
            // don't ask activate page whether it is valid...hence 'length-1'
            var page, isValid;
            for (var i = 0; i < this.pageKeys.length-1; i++) {
                page = this.pages[this.pageKeys[i]];
                isValid = page.isValid();
                this.navbarView.markPage(this.pageKeys[i], isValid);
                if (!isValid) {
                    this.navbarView.disable("activate");
                    return;
                }
            }
            // if still here, everything is valid...enable activate
            this.navbarView.enable("activate");
        },
        
        onMappingSaved: function () {
            this.validatePages();
        }


        
    });

    this.show = function() {
        this.headerView = new this.views.HeaderView();
        
        App.header.show(this.headerView);

        App.Config = new Main.controllers.ConfigurationController();
        App.body1.show(App.Config.view);
    };

    this.onTemplatesLoaded = function() {
        this.show();
    };
});