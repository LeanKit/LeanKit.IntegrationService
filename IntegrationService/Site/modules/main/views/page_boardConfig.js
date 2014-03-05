/*
//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------
*/
App.module("Main", function (Main, App, Backbone, Marionette, $, _) {


    Main.controllers.BoardConfigurationController = Marionette.Controller.extend({
        initialize: function(options) {
            this.owner = options.owner;
            this.pageName = options.pageName;
            this.mappingCollection = options.model;
            this.loadedConfigurableFields = false;
            this.loadedFilterFields = false;
            App.credentials = options.credentials;
            
            // configure request handlers and events
            App.reqres.setHandler("getStates", this.getStates, this);
            App.reqres.setHandler("getFilterPaths", this.getFilterPaths, this);
            App.reqres.setHandler("saveCurrentMapping", this.save, this);
            this.listenTo(this.mappingCollection, "destroy", this.resetMappingView, this);

            // kick off board loading
            this.loadBoardsAndProjects();

            // for debugging...
            App.bc = this;
        },

        isValid: function() {

            if (!_.isObject(this.mappingCollection) || this.mappingCollection.length === 0) return false;

            var invalidMappings = 0;
            this.mappingCollection.each(function(mapping) {
                if (!mapping.isValid()) invalidMappings++;
            });

            return invalidMappings === 0;
        },

        loadBoardsAndProjects: function () {
            if (_.isObject(App.credentials.target) && _.isString(App.credentials.target.Host()))
                this.loadConfigurableFieldsTask = this.loadConfigurableFields();
            if (_.isObject(App.credentials.target) && _.isString(App.credentials.target.Host()))
                this.loadFilterFields();
            if (_.isObject(App.credentials.leankit) && _.isString(App.credentials.leankit.Host()))
                this.loadBoardsTask = this.loadBoards();
            if (_.isObject(App.credentials.target) && _.isString(App.credentials.target.Host()))
                this.loadProjectsTask = this.loadProjects();
        },
        
        onPageShow: function() {
            this.view = new Main.views.BoardConfigurationView({ controller: this });
        },

        onPageLeave: function() {
            this.save();
            this.view.close();
            this.view = null;
        },

        onShow: function () {
            if (_.isUndefined(this.loadBoardsTask)
                || _.isUndefined(this.loadProjectsTask)
                || _.isUndefined(this.loadConfigurableFieldsTask)
                || !(this.loadedFilterFields))
                this.loadBoardsAndProjects();
            
            if (this.loadBoardsTask.state() === 'resolved'
                && this.loadProjectsTask.state() === 'resolved'
                && this.loadConfigurableFieldsTask.state() === 'resolved'
                && !(this.loadedFilterFields))
                this.buildBoardList();
            else {
                // restart if there was a previous error
                if (this.loadBoardsTask.state() === 'rejected') {
                    this.loadBoardsTask = this.loadBoards();
                } else if (this.loadProjectsTask.state() === 'rejected') {
                    this.loadProjectsTask = this.loadProjects();
                } else if (this.loadConfigurableFieldsTask.state() === 'rejected') {
                    this.loadConfigurableFieldsTask = this.loadConfigurableFields();
                } else if (!(this.loadedFilterFields)) {
                    this.loadFilterFields();
                }

                // whether restarted or not, wait for completion
                $.when(this.loadConfigurableFieldsTask).done(this.buildBoardList);
                $.when(this.loadProjectsTask).done(this.buildBoardList);
                $.when(this.loadBoardsTask).done(this.buildBoardList);
                
            }

        },

        loadBoards: function() {
            this.boards = new Main.models.BoardCollection();

            return this.boards.fetch({
                data: $.param(App.credentials.leankit.attributes),
                context: this,
                error:function () {
                   alert("Load LeanKit Boards failed, please verify your settings on the 'Connect to LeanKit' tab.");
                }
            });
        },

        loadProjects: function() {
            Main.targetProjects = this.targetProjects = new Main.models.ProjectCollection();
            return this.targetProjects.fetch({
                data: $.param(App.credentials.target.attributes),
                context: this,
                error:function () {
                    alert("Load Target Projects failed, please verify settings for type and credentials on the 'Connect to Target' tab.");
            }
        });
        },
        
        loadConfigurableFields: function () {
            Main.configurableFields = this.configurableFields = new Main.models.ConfigurableFieldsCollection();
            return this.configurableFields.fetch({
                data: $.param(App.credentials.target.attributes),
                context: this,
                success: function (collection, response, options) {
                    options.context.loadedConfigurableFields = true;
                },
                error: function () {
                    alert("Load Configurable Fields failed, please verify settings for type and credentials on the 'Connect to Target' tab.");
                }
            });
        },
        
        loadFilterFields: function() {
            $.ajax({
                url: "/filterfields",
                data: $.param(App.credentials.target.attributes),
                context: this,
                contentType: "application/json",
                success: function (result) {
                    this.filterFields = result;
                    this.loadedFilterFields = true;
                },
                error: function () {
                    alert("Load Filter Fields failed.");
                }
            });
        },

        buildBoardList: function(collection, result) {
            // wait for both projects and boards to finish loading!
            if (_.isUndefined(this.boards) || this.boards.length === 0) return;
            if (_.isUndefined(this.targetProjects) || this.targetProjects.length === 0) return;
            if (_.isUndefined(this.configurableFields)) return;
            if (_.isUndefined(this.filterFields)) return;
            if (!this.loadedConfigurableFields) return;

            for (var j = 0; j < this.mappingCollection.length; j++) {
                this.updateFieldMappingConfiguration(this.mappingCollection.at(j));                
            }

            if (this.configurableFields.length > 0) {
                var defaultCardType = this.configurableFields.findWhere({ LeanKitField: "CardType" });
                if (!_.isUndefined(defaultCardType)) {                    
                    var defaultCardTypeField = _.find(defaultCardType.TargetFields(), function (itm) {
                        return itm.IsDefault;
                    });
                    if (!_.isUndefined(defaultCardTypeField)) {
                        for (var k = 0; k < this.targetProjects.length; k++) {
                            // build list of types for each project based on default configuration for CardType;
                            this.updateProjectTypes(this.targetProjects.at(k).Id(), defaultCardTypeField.Name);
                        }
                    }
                }   
            }

            for (var i = 0; i < this.boards.length; i++) {
                var board = this.boards.at(i);
                var configuredMap = this.mappingCollection.findWhere({ BoardId: board.get("Id") });
                if (_.isObject(configuredMap)) {
                    var project = this.getMappedProject(configuredMap);
                    if (_.isUndefined(project)) {
                        App.log("Error -- could not match project from configuration.");
                    } else {                        
                        board.TargetProjectId(project.Id());
                        board.TargetProjectName(project.Name());
                        this.updateProjectTypesForMapping(project.Id(), configuredMap);
                    }
                } else {
                    board.unset("TargetProjectId", { silent: true });
                    board.unset("TargetProjectName", { silent: true });
                }
            }
            this.view.displayBoardList(this.boards);
        },
        
        updateProjectTypes: function (projectId, targetFieldName) {
            if (this.configurableFields.length > 0) {
                var parms = $.param(App.Config.configuration.Target().attributes);
                parms += "&project=" + projectId + "&field=" + targetFieldName;
                $.ajax({
                    url: "/tasktypes",
                    data: parms,
                    context: this,
                    contentType: "application/json",
                    success: function(result) {
                        var thisProj = this.targetProjects.findWhere({ Id: projectId });
                        if (_.isObject(thisProj) && !_.isUndefined(thisProj)) {
                            thisProj.set("Types", result);
                            this.trigger('projectTypesUpdated', projectId, result);
                        }
                    },
                    error: function() {
                        alert("Getting project types failed.");
                    }
                });
            }
        },

        updateProjectTypesForMapping: function(projectId, boardMapping) {
            var cardTypeMap = boardMapping.get("FieldMap").findWhere({ LeanKitField: "CardType" });
            if (_.isObject(cardTypeMap) && !_.isUndefined(cardTypeMap)) {
                var targetField = cardTypeMap.get("TargetFields").findWhere({ IsSelected: true });
                if (_.isObject(targetField) && !_.isUndefined(targetField)) {
                    this.updateProjectTypes(projectId, targetField.Name());
                }
            }
        },

        updateFieldMappingConfiguration: function(boardMapping) {
            var fieldMapConfiguration = boardMapping.get("FieldMap");                
            if (fieldMapConfiguration.length == 0) {
                // create a new one from configurable fields  
                for (var k = 0; k < this.configurableFields.length; k++) {
                    fieldMapConfiguration.add(new Main.models.FieldMapModel(
                        {
                            LeanKitField: this.configurableFields.at(k).get("LeanKitField"),
                            SyncDirection: this.configurableFields.at(k).get("DefaultSyncDirection"),
                            SyncDirections: this.configurableFields.at(k).get("SyncDirections"),
                            TargetFields: this.configurableFields.at(k).get("TargetFields")
                        }));
                }
            } else {
                // update existing field mapping configuration    
                this.configurableFields.each(function (confFieldItem) {
                    var fieldMapItem = fieldMapConfiguration.findWhere({ LeanKitField: confFieldItem.get("LeanKitField") });
                    // if a field does not exist in the configuration but does in configurable fields 
                    // then add it to the configuration                          
                    if (!_.isObject(fieldMapItem) || fieldMapItem === undefined || fieldMapItem === null) {
                        fieldMapConfiguration.add(new Main.models.FieldMapModel(
                            {
                                LeanKitField: confFieldItem.LeanKitField(),
                                SyncDirection: confFieldItem.DefaultSyncDirection(),
                                SyncDirections: confFieldItem.SyncDirections(),
                                TargetFields: confFieldItem.TargetFields()
                            }));                            
                    }
                });

                // loop through any existing field mapping
                fieldMapConfiguration.each(function (item) {
                    var configurableFieldItem = this.configurableFields.findWhere({ LeanKitField: item.get("LeanKitField") });                        
                    if (!_.isObject(configurableFieldItem) || configurableFieldItem === undefined || configurableFieldItem === null) {
                        // the field mapping exists in configuration but not in configurable fields so remove it
                        fieldMapConfiguration.remove(item);
                    } else {
                        // The field exists in configuration
                        // Update the sync directions
                        if (_.isUndefined(item.SyncDirections()) || item.SyncDirections().length === 0) {
                            // add them all if the field doesn't have any
                            item.SyncDirections(configurableFieldItem.SyncDirections());
                        } else {
                            // add any that are missing
                            for (var i = 0; i < configurableFieldItem.SyncDirections().length; i++) {
                                var confSyncDirectionIdx = item.SyncDirections().indexOf(configurableFieldItem.SyncDirections()[i]);
                                if (confSyncDirectionIdx < 0) {
                                    // add the sync direction
                                    var newSyncDirection = configurableFieldItem.SyncDirections()[i];
                                    item.SyncDirections().push(newSyncDirection);
                                }
                            }
                        }                        
                        // Check the target fields
                        var configurableTargetFieldItems = configurableFieldItem.TargetFields();
                        var targetFieldsConfiguration = item.get("TargetFields");
                        // remove any Target Fields included but not in configurable fields 
                        _.each(targetFieldsConfiguration.models, function (field) {                                
                            var confTargetFieldItem = _.find(configurableTargetFieldItems, function (itm) {
                                return itm.Name === field.get("Name");
                            });
                            if (!_.isObject(confTargetFieldItem) || confTargetFieldItem === undefined || confTargetFieldItem === null) {
                                targetFieldsConfiguration.remove(field);
                            }
                        }, this);
                        // add any Target Fields not already included   
                        _.each(configurableTargetFieldItems, function (confTargetFieldItem) {
                            var targetField = _.find(targetFieldsConfiguration.models, function (itm) {
                                return itm.get("Name") === confTargetFieldItem.Name;
                            });
                            if (!_.isObject(targetField) || targetField === undefined || targetField === null) {
                                targetFieldsConfiguration.add(new Main.models.TargetFieldMapModel(
                                    {
                                        Name: confTargetFieldItem.Name,
                                        IsDefault: confTargetFieldItem.IsDefault,
                                        IsSelected: false
                                    }));
                            }
                        });
                    }                        
                }, this);
            }
            fieldMapConfiguration.each(function (item) {
                var isSelected = item.get("TargetFields").findWhere({ IsSelected: true });
                if (_.isUndefined(isSelected)) {
                    var isDefault = item.get("TargetFields").findWhere({ IsDefault: true });
                    if (!_.isUndefined(isDefault)) {
                        isDefault.IsSelected(true);
                    }
                }
            });
        },

        getMappedProject: function(mapping) {
            var tpid = mapping.get("TargetProjectId");
            var proj = this.targetProjects.findWhere({ Id: tpid });
            if (_.isObject(proj)) return proj;

            // no match for id; almost certainly because the mapping comes from config.txt
            // try to match by name

            var name = tpid;
            var i = name.indexOf("\\");
            if (i > 0) {
                name = name.substring(0, i);
            }
            proj = this.targetProjects.findWhere({ Name: name });

            if (_.isUndefined(proj)) {
                mapping.destroy();
                alert('Unable to find a project matching [' + name + ']. This mapping was skipped and will need to be re-configured.');
            } else {
                // fix the project Id & name
                mapping.TargetProjectId(proj.Id());
                mapping.TargetProjectName(proj.Name());
            }
            
            return proj;                        
        },

        requestDetailView: function(id, context) {
            // look up configuration for this board
            var dfd = $.Deferred();
            id = parseInt(id);
            var board = this.boards.findWhere({ Id: id });
            this.currentMapping = this.mappingCollection.findWhere({ BoardId: id });

            if (_.isObject(this.currentMapping)) {
                var tpid = this.currentMapping.TargetProjectId();
                var project = this.targetProjects.findWhere({ Id: tpid });
                this.currentMapping.TargetProjectName(project.Name(), true);
                this.currentMapping.Title(board.Title(), true);
                this.cachedStates = project.get("States");
                this.cachedTypes = project.get("Types");
                this.cachedFilterPaths = project.get("cachedFilterPaths");
            } else {
                this.currentMapping = new Main.models.BoardMapping({ BoardId: id, Title: board.get("Title"), TargetProjectId: "" });
                this.updateFieldMappingConfiguration(this.currentMapping);
                this.cachedStates = this.cachedTypes = this.cachedFilterPaths = undefined;
            }

            // subscribe to this mapping
            this.listenTo(this.currentMapping, "destroy", this.currentMappingDestroyed, this);
            this.listenTo(this.currentMapping, "change:TargetProjectId", this.targetProjectIdChanged, this);

            // load detail for this board.
            this.closeDetails();
            this.detailController = new Main.controllers.MappingDetailController({ owner: this, model: this.currentMapping, filterfields: this.filterFields });
            var viewOrPromise = this.detailController.view;
            if (viewOrPromise.state && viewOrPromise.state() === "pending") {
                $.when(viewOrPromise).done(function() { dfd.resolveWith(context); });
            } else {
                dfd.resolveWith(context);
            }
            return dfd;

        },

        closeDetails: function() {
            if (_.isObject(this.detailController)) {
                this.detailController.close();
                this.detailController = undefined;
            }
        },

        resetMappingView: function() {
            this.closeDetails();
            this.buildBoardList();
        },

        getAvailableTargetProjects: function() {
            // return list of targetProjects, minus those in mappingCollection
            var availableProjects = new Backbone.Collection(this.targetProjects.models);

            // add blank
            availableProjects.add(new Backbone.Model({ Id: "", Name: "" }), { at: 0 });

            return availableProjects;
        },

        getDetailView: function() {
            return this.detailController.view;
        },

        onBoardLoaded: function() {
            this.view.displayDetailView();
        },

        getStates: function() {
            if (_.isObject(this.cachedStates)) return this.cachedStates;

            // look up using currentMapping
            if (_.isObject(this.currentMapping)) {
                var project = this.targetProjects.findWhere({ Id: this.currentMapping.TargetProjectId() });
                if (_.isObject(project)) {
                    this.cachedStates = project.get("States");
                    return this.cachedStates;
                }
            }

            return [];
        },

        getTypes: function() {
            if (_.isObject(this.cachedTypes)) return this.cachedTypes;

            // look up using currentMapping
            if (_.isObject(this.currentMapping)) {
                var project = this.targetProjects.findWhere({ Id: this.currentMapping.TargetProjectId() });
                if (_.isObject(project)) {
                    this.cachedTypes = project.get("Types");
                    return this.cachedTypes;
                }
            }

            return [];
        },

        getFilterPaths: function() {
            if (_.isObject(this.cachedFilterPaths)) return this.cachedFilterPaths;

            // look up using currentMapping
            if (_.isObject(this.currentMapping)) {
                var project = this.targetProjects.findWhere({ Id: this.currentMapping.TargetProjectId() });
                if (_.isObject(project)) {
                    this.cachedFilterPaths = project.get("PathFilters");
                    return this.cachedFilterPaths;
                }
            }

            return [];
        },

        save: function() {
            if (_.isObject(this.currentMapping) && this.currentMapping.isDirty) {
                var pageController = this.owner;
                this.currentMapping.save(null, {
                    success: function() {
                        pageController.triggerMethod("mapping:saved");
                    },
                    context: this
                });
                this.currentMapping.isDirty = false;
                return true;
            }
        },

        currentMappingDestroyed: function() {
            if (_.isObject(this.currentMapping)) {
                this.stopListening(this.currentMapping);
                this.currentMapping = undefined;
            }
        },

        onProjectAssigned: function() {
            this.mappingCollection.add(this.currentMapping);
            // update corresponding item in board list
            var board = this.boards.findWhere({ Id: this.currentMapping.BoardId() });
            board.TargetProjectId(this.currentMapping.TargetProjectId());
            board.TargetProjectName(this.currentMapping.TargetProjectName());

            this.view.displayBoardList(this.boards, board.Id());
            App.Config.loadLaneNames();
        },

        targetProjectIdChanged: function() {
            // project assigned to a board; add it to the list
        }
    });

    Main.views.BoardConfigurationView = Main.views.PageView.extend({
        template: this.template("page_boardConfig"),
        className: "panel panel-primary",
        events: {
            "click a.list-group-item": "boardSelected"
        },

        regions: {
            "details": "#mapping"
        },

        initialize: function(options) {
            this.controller = options.controller;
        },

        ui: {
            "message": "#message",
            "connect": "#btn-connect",
            "spinner": "#spinner",
            "panelbar": "#panelbar",
            "boardList": "#board-list",
            "mapping": "#mapping"
        },

        onShow: function() {
            this.controller.onShow();
            this.delegateEvents();

        },

        displayBoardList: function (collection, selectedId) {
            collection.sort();
            if (_.isObject(this.boardCollectionView)) {
                this.boardCollectionView.close();
            }
            
            this.boardCollectionView = new Main.views.BoardListView({ collection: collection });
            this.boardCollectionView.render();

            this.ui.boardList.html(this.boardCollectionView.el);
            if (selectedId) {
                this.$("a#" + selectedId).addClass('active');
            }
        },

        boardSelected: function(e) {
            this.$(e.currentTarget.parentElement.children).removeClass('active');
            this.$(e.currentTarget).addClass('active');
            this.controller.save();
            $.when(this.controller.requestDetailView(e.currentTarget.id, this))
                .done(this.displayDetailView);
        },

        displayDetailView: function() {
            this.details.show(this.controller.getDetailView());
        }
    });

});
