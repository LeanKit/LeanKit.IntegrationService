
String.format = function (pattern, args) {
    if (arguments.length < 2) return pattern;
    for (var i = 0; i < arguments.length - 1; i++) {
        var reg = new RegExp("\\{" + i + "\\}", "gm");
        pattern = pattern.replace(reg, arguments[i + 1]);
    }
    return pattern;
};

String.prototype.pad = function (len, c) {
    var s = this, c = c || '0';
    while (s.length < len) s = c + s;
    return s;
};

String.prototype.toId = function (includeHashSymbol) {
    if (_.isUndefined(includeHashSymbol)) includeHashSymbol = true;
    var str = this;
    // convert to lowercase
    str = str.toLowerCase();
    // replace spaces with dashes
    str = str.replace(/ /g, "-");
    
    if (includeHashSymbol) str = "#" + str;
    
    return str;
};

String.prototype.fromId = function () {
    var str = this;
    // replace dashes with spaces
    str = str.replace(/-/g, " ");

    // trim and convert to Uppercase first letter
    str = str.replace(/\w\S*/g, function(txt) {
        return txt.charAt(0).toUpperCase() + txt.substr(1);
    });
    return str;
};

NiceTools = (function(Backbone, Marionette, _) {
    "use strict";
    var niceTools = { };


    niceTools.AppRouter = Marionette.AppRouter.extend({
        initialize: function() {
            this.bind('route', this.trackPageView);
        },
        trackPageView: function(url) {
            if (!url) url = Backbone.history.getFragment();
            if (!/^\//.test(url) && url != "") {
                url = "/" + url;
            }

            if (typeof _gaq !== 'undefined') _gaq.push(['_trackPageview', url]);
        }
    });

    niceTools.ViewFactory = (function(Backbone, Marionette, _) {
        "use strict";

        // Constructor
        // -----------

        var ViewFactory = function (controller, options) {
            Backbone.ChildViewContainer.prototype.constructor.apply(this);
            this.controller = controller;
            if(_.isString(options)) {
                this.options = {};
                this.id = options;
            } else
                this.options = options || {};
            
            this._factories = { };

            if (_.isFunction(this.initialize)) {
                this.initialize(options);
            }

        };

        ViewFactory.extend = Backbone.Model.extend;

        // Instance Members
        // ----------------

        _.extend(ViewFactory.prototype, Backbone.ChildViewContainer.prototype, {
            register: function(customId, viewFn) {
                // add a view factory function
                this._factories[customId] = viewFn;
            },
            createView: function(customId) {
                var vFn = this._factories[customId];
                if (_.isObject(vFn)) {
                    var v = this.findByCustom(customId);
                    if(_.isObject(v)) {
                        v.close();
                        this.remove(v);
                    }
                    var view = vFn(this.controller);
                    this.add(view, customId);
                    
                    // assign the id to the view
                    view.id = customId;

                    // if there is a controller view, and it's a Layout, create a region
                    if (_.isObject(this.controller.view) && (_.isFunction(this.controller.view.addRegion))) {
                        this.controller.view.addRegion(customId, "#" + customId);
                    }
                    
                    return view;
                }
                return undefined;
            },
            
            close:function () {
                _.each(this._views, function (view) {
                    view.close();
                    this.remove(view);
                }, this);
            }
        });
        return ViewFactory;
    })(Backbone, Marionette, _);

    niceTools.Controller = Marionette.Controller.extend({
        onPrepViews: function () {
            this.viewFactory.each(function (view) {
                if (_.isString(view.id)) {
                    var id = "#" + view.id;
                    var $el = this.view.$(id);
                    if (_.isObject($el)) {
                        view.$el = $el;
                        view.bindUIElements();
                        view.delegateEvents();
                    }
                }
            }, this);
        },
        
        onPrepNestedViews: function () {
            this.viewFactory.each(function (nestedView) {
                if (_.isString(nestedView.id)) {
                    var region = this.view[nestedView.id];
                    region.show(nestedView);
                }
            }, this);
        },


    });
    


    niceTools.Model = Backbone.Model.extend({
        toJSON: function() {
            // adds nested support to the model
            return $.extend(true, { }, this.attributes);
        },
        collapse: function () {
            var collapsed = this.attributes;
            for (var key in collapsed) {
                var prop = collapsed[key];
                if (prop.hasOwnProperty('attributes')) {
                    collapsed[key] = prop.collapse();
                }
            }
            return collapsed;
        }

        //save: function (attr, options) {
        //    // add api token to header
        //    options = options || {};
        //    options.headers = options.headers || {};
        //    options.headers['api-token'] = App.ApiToken;
        //    Backbone.Model.prototype.save.call(this, attr, options);
        //},
        //fetch: function (options) {
        //    // add api token to header
        //    options = options || {};
        //    options.headers = options.headers || {};
        //    options.headers['api-token'] = App.ApiToken;
        //    Backbone.Model.prototype.fetch.apply(this, arguments);
        //}
    });

    niceTools.AuthorizationModel = niceTools.Model.extend({
        permissions: 0,
        constructor: function(initialPermissions) {
            if (initialPermissions)
                this.permissions = initialPermissions;
            Backbone.Model.prototype.constructor.apply(this, arguments);
        },
        has: function(permCode) {
            return ((this.permissions & permCode) == permCode);
        },
        grant: function(permCode) {
            this.permissions = this.permissions | permCode;
        },

        revoke: function(permCode) {
            this.permissions = this.permissions & ~permCode;
        },

        toggle: function(permCode) {
            this.permissions = this.permissions ^ permCode;
        }
    });


    niceTools.PagedCollection = Backbone.Collection.extend({
        parse: function(json) {
            this.totalItems = json.TotalItems;
            this.totalPages = json.TotalPages;
            this.pageNumber = json.PageNumber;
            return json.Items;
        }
    });

    niceTools.PUT = function(url, data, context) {
        var dfd = $.Deferred();
        $.ajax(url, {
            data: data,
            type: 'PUT',
            context: context,
            //headers: { 'api-token': App.ApiToken },
            complete: function(result) {
                if (result.status === 200)
                    dfd.resolveWith(this);
                else
                    dfd.rejectWith(this, [result.statusText]);
            }
        });
        return dfd;
    };

    niceTools.GetFeed = function(feedUrl) {
        var dfd = $.Deferred();
        var url = "https://ajax.googleapis.com/ajax/services/feed/load?q={0}&v=1.0&callback="
    };

    niceTools.ItemView = Marionette.ItemView.extend({
        // add short cut to get/set model properties
        M: function(prop, val) {
            if (!this.model) return;
            if (val == undefined)
                return this.model.get(prop);
            else
                this.model.set(prop, val, { silent: false });
        },
        setButtonEnabled: function(selector, test) {
            var btn = this.$(selector);
            if (btn && btn.length > 0)
                if (test) btn.removeAttr('disabled');
                else btn.attr('disabled', '');
        },
        setItemVisibility: function(selectorOrEl, test) {
            var el;
            if (typeof(selectorOrEl) === 'string')
                el = this.$(selectorOrEl);
            else
                el = selectorOrEl;
            if (el)
                if (test) {
                    el.show();
                    if (el.css('display') === 'block') el.removeAttr('style');
                } else el.hide();
        },
        setCheckbox: function(prop) {
            if (!prop) return;
            var selector = 'input#' + prop;
            var cb = this.$(selector);
            if (!cb || cb.length == 0) return;
            if (this.M(prop)) cb.attr('checked', '');
            else cb.removeAttr('checked');
        }
    });

    niceTools.BoundViewMixIn = {

        initializeBindings:function () {
            if (!this.events) this.events = {};
            this.events["change input[type=text]"] = "fieldChanged";
            this.events["change fieldset input[type=password]"] = "fieldChanged";
            this.events["change fieldset input[type=checkbox]"] = "checkboxChanged";
            this.events["change fieldset textarea"] = "fieldChanged";
            this.events["change fieldset select"] = "selectChanged";
            this.events["keypress fieldset input"] = "inputKeypressed";
            this.fadeHtmlChanges = true;
        },
        
        bindModel: function() {
            // call after rendering to populate fields with model values
            for (var prop in this.model.attributes) {
                if (prop === "") continue;
                var el = this.$("#" + prop);
                var newVal = this.model.get(prop);
                if (el && el.length > 0) {
                    this.updateElement(el, newVal);
                }
            }

            // watch changes on the model
            this.listenTo(this.model, "change", this.modelChanged, this);

        },

        fieldChanged: function(e) {
            if (e.currentTarget.id != "") {
                this.model.lastChangedElement = e.currentTarget.id;
                this.model.set(e.currentTarget.id, e.currentTarget.value);
            }
        },

        checkboxChanged: function(e) {
            if (e.currentTarget.id != "") {
                this.model.lastChangedElement = e.currentTarget.id;
                this.model.set(e.currentTarget.id, e.currentTarget.checked);
            }
        },

        selectChanged: function(e) {
            var type = e.currentTarget.type;
            var id;
            var selectedText;
            if (type === "select-one") {
                if (e.currentTarget.id != "") {
                    id = e.currentTarget.value;
                    this.model.set(e.currentTarget.id, id);
                }
                selectedText = e.currentTarget.options[e.currentTarget.selectedIndex].text;
            } else {
                // multi-select types are not supported at this time
            }

            this.triggerMethod("item:selected", id, selectedText, e.currentTarget);
        },

        modelChanged: function(m) {
            for (var prop in m.changed) {
                if (prop === "") continue;

                this.triggerMethod('change', m);

                if (prop === this.model.lastChangedElement) {
                    // drop out because the element changing is what raised this event
                    this.model.lastChangedElement = undefined;
                    return;
                }
                var el = this.$("#" + prop);
                var newVal = m.changed[prop];
                if (el && el.length > 0) {
                    this.updateElement(el, newVal);
                }
            }
        },

        updateElement: function(el, newVal) {
            if (el.is('input')) {
                if (el[0].type === "checkbox")
                    el.prop('checked', newVal);
                else
                    el.val(newVal);
            } else if (el.is('select')) {
                el.val(newVal);
            } else {
                if (this.fadeHtmlChanges) {
                    el.fadeOut(250, '', function(x) {
                        el.html(newVal);
                        el.fadeIn(250);
                    });
                } else {
                    el.html(newVal);
                }
            }
        },
        inputKeypressed: function(e) {
            if (e.which === 13) {
                var el = e.currentTarget;
                var $el = this.$(el);
                // check for changes on current target
                if ($el.is('input') || $el.is('select')) {
                    if (this.model.set(el.id) !== el.value)
                        this.model.set(el.id, el.value);
                }
                e.preventDefault();
                if (this.userPressedEnter) this.userPressedEnter();
            }
        }
    };

    niceTools.BoundView = niceTools.ItemView.extend(niceTools.BoundViewMixIn);

    niceTools.NestedView = niceTools.BoundView.extend({
        initialize: function (options) {
            this.controller = options.controller;
            initializeBindings();
            this.triggerMethod("after:initialize", options);

        },
        onRender: function () {
            if (this.controller) this.controller.triggerMethod("prep:views");
            this.triggerMethod("rendered");
        }
    });

    niceTools.ModalView = niceTools.BoundView.extend({
        constructor: function() {
            if (!this.events) this.events = { };
            this.events["click #cancelButton"] = "defaultCancelRequested";
            this.events["click #actionButton"] = "defaultActionRequested";
            if (!this.ui) this.ui = { };
            this.ui.cancelBtn = "#cancelButton";
            this.ui.actionBtn = "#actionButton";
            this.on('before:render', this.beforeRender, this);
            niceTools.BoundView.prototype.constructor.apply(this, arguments);
        },

        className: "modal",

        beforeRender: function() {
            if (this.wide) this.$el.addClass("modal-wide");
            //Marionette.Renderer.prefixHtml = "<div class='modal-header'><button type='button' class='close' data-dismiss='modal'>×</button><h3></h3></div><div class='modal-body'>";
            //Marionette.Renderer.suffixHtml = "</div><div class='modal-footer'><div  id='cancelButton' class='btn' data-dismiss='modal'>Cancel</div><div id='actionButton' class='btn btn-warning'>Save changes</div></div>";
        },

        onChange: function(m) {
            this.enable();
        },

        onShow: function() {
            this.$el.parent().modal({ show: true });
            if (this.afterShow) this.afterShow();
            this.disable();
        },

        enable: function() {
            this.ui.actionBtn.removeClass("disabled");
            this.actionDisabled = false;
        },

        disable: function() {
            this.ui.actionBtn.addClass("disabled");
            this.actionDisabled = true;
        },

        defaultActionRequested: function() {
            if (this.actionDisabled) return;
            if (this.actionRequested) this.actionRequested();
            else this.defaultAction();
        },

        defaultAction: function() {
            // default action is to save the associated model
            this.listenTo(this.model, 'sync', this.saved, this);
            this.listenTo(this.model, 'error', this.saveFailed, this);
            this.model.save();
        },

        defaultCancelRequested: function() {
            if (this.cancelRequested) this.cancelRequested();
        },

        saved: function() {
            this.model.off('sync', this.saved, this);
            if (this.onSaved) this.onSaved();
            this.close();
        },

        saveFailed: function(model, response) {
            if (this.onSaveFailed) this.onSaveFailed(response.responseText);
        },

        close: function() {
            if (this.onClose) this.onClose();
            this.$el.parent().modal("hide");
        }
    });

    niceTools.ConfirmationView = Marionette.ItemView.extend({
        template: "#confirm",

        constructor: function(options) {
            Marionette.ItemView.prototype.constructor.apply(this, arguments);
            this.target = options.target;
            this.identifier = options.identifier;
            this.target.listenTo(this, 'confirmed', this.target.confirmed, this.target);
        },

        events: {
            "click #cancel-btn": "cancelRequested",
            "click #action-btn": "actionRequested",
            "click #close-btn": "cancelRequested"
        },

        className: "modal",

        onShow: function() {
            this.$el.parent().show();
        },

        cancelRequested: function() {
            this.close();
            this.target.stopListening(this, 'confirmed');
        },

        actionRequested: function() {
            this.trigger('confirmed', this.identifier);
            this.target.stopListening(this, 'confirmed');
            this.close();
        }
    });

    niceTools.PartialView = Marionette.Layout.extend({
        render: function() {
            //noop
            if (this.onRender) {
                this.onRender();
            }
            return this;
        },
        loadHtml: function(parms, obj) {
            if (!this.url) return;
            $.ajax({
                type: 'get',
                url: this.url,
                data: parms,
                context: this,
                success: function(result) {
                    this.$el.html(result);
                    this.render();
                    obj && obj.loadHtmlCompleted && obj.loadHtmlCompleted();
                }
            });
        },
        setHtml: function(html) {
            this.$el.html(html);
        },
        onShow: function() {
            if (this.url)
                this.loadHtml();
            // make sure events are ready
            this.delegateEvents();
        }
    });
    return niceTools;
})(Backbone, Marionette, _);
