

(function(Marionette, _) {
    "use strict";
    var niceViews = {};
    

    return niceViews;
})(Marionette, _);


//// just a shortcut...
//function log(str) {
//    if (console) console.log(str);
//}
//if (!window.console) (function () {

//    var __console, Console;

//    Console = function () {
//        var check = setInterval(function () {
//            var f;
//            if (window.console && console.log && !console.__buffer) {
//                clearInterval(check);
//                f = (Function.prototype.bind) ? Function.prototype.bind.call(console.log, console) : console.log;
//                for (var i = 0; i < __console.__buffer.length; i++) f.apply(console, __console.__buffer[i]);
//            }
//        }, 1000);

//        function log() {
//            this.__buffer.push(arguments);
//        }

//        this.log = log;
//        this.error = log;
//        this.warn = log;
//        this.info = log;
//        this.__buffer = [];
//    };

//    __console = window.console = new Console();
//})();

//var JDM = JDM || {};
//var codegen = {};
//codegen.models = {};



JDM.AppRouter = Marionette.AppRouter.extend({
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

// extend Marionette.Region instance methods adding stash and restore
_.extend(Marionette.Region.prototype, Backbone.Events, {
    show: function (view) {

        if (typeof view === 'undefined' && this.currentView) {
            if (this.isVisible) {
                Marionette.triggerMethod.call(this.currentView, "refresh");
                return;
            }
            this.$el.show();
            Marionette.triggerMethod.call(this.currentView, "show");
            return;
        }

        this.ensureEl();
        this.close();

        if (view === this.stashedView) {
            this.restore();
        } else {
            view.render();
            this.open(view);

            Marionette.triggerMethod.call(view, "show");
            Marionette.triggerMethod.call(this, "show", view);

            this.currentView = view;
        }
        this.isVisible = true;
    },
    
    hide:function () {
        this.$el.hide();
        this.isVisible = false;
        if(this.currentView) {
            Marionette.triggerMethod.call(this.currentView, "hide");
        }
    },

    // Close the current view, if there is one. If there is no
    // current view, it does nothing and returns immediately.
    close: function () {
        var view = this.currentView;
        if (!view || view.isClosed) { return; }

        if (this.stashedView !== this.currentView) {
            if (view.close) {
                view.close();
            }
            Marionette.triggerMethod.call(this, "close");
        }
        delete this.currentView;
    },

    stash: function (view) {
        if (view) {
            this.stashedView = view;
            Marionette.triggerMethod.call(view, "stash");
        } else {
            if (!this.currentView) return;
            this.stashedView = this.currentView;
            Marionette.triggerMethod.call(this.currentView, "stash");
        }
    },

    restore: function () {
        if (!this.stashedView) return;
        
        this.open(this.stashedView);
        this.currentView = this.stashedView;
        
        Marionette.triggerMethod.call(this.currentView, "restore");

        delete this.stashedView;
    }
});

JDM.deParam = function(str) {
    var addSegment = function(obj, segmentStr) {
        if (obj) {
            var endOfName = segmentStr.indexOf('=');
            var name = segmentStr.substring(0, endOfName);
            var value = segmentStr.substring(endOfName + 1);
            obj[name] = value;
        }
    }

    if (!str || str.length === 0) return undefined;

    var result = {};

    var startOfSegment = (str[0] === '?') ? 1 : 0;
    var endOfSegment = str.indexOf("&");
    if (endOfSegment === -1) endOfSegment = str.length;
    while (startOfSegment < str.length) {
        addSegment(result, str.substring(startOfSegment, endOfSegment));
        startOfSegment = endOfSegment + 1;
        endOfSegment = str.indexOf('&', startOfSegment);
        if (endOfSegment === -1) endOfSegment = str.length;
    }

    return result;
};

JDM.Model = Backbone.Model.extend({
    toJSON: function () {
        // adds nested support to the model
        return $.extend(true, {}, this.attributes);
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

JDM.AuthorizationModel = JDM.Model.extend({
    permissions: 0,
    constructor: function (initialPermissions) {
        if (initialPermissions)
            this.permissions = initialPermissions;
        Backbone.Model.prototype.constructor.apply(this, arguments);
    },
    has: function (permCode) {
        return ((this.permissions & permCode) == permCode);
    },
    grant: function (permCode) {
        this.permissions = this.permissions | permCode;
    },

    revoke: function (permCode) {
        this.permissions = this.permissions & ~permCode;
    },

    toggle: function (permCode) {
        this.permissions = this.permissions ^ permCode;
    }
});


JDM.PagedCollection = Backbone.Collection.extend({
    parse: function (json) {
        this.totalItems = json.TotalItems;
        this.totalPages = json.TotalPages;
        this.pageNumber = json.PageNumber;
        return json.Items;
    }
});

JDM.PUT = function (url, data, context) {
    var dfd = $.Deferred();
    $.ajax(url, {
        data: data,
        type: 'PUT',
        context: context,
        headers: { 'api-token': App.ApiToken },
        complete: function (result) {
            if (result.status === 200)
                dfd.resolveWith(this);
            else
                dfd.rejectWith(this, [result.statusText]);
        }
    });
    return dfd;
}

JDM.GetFeed = function (feedUrl) {
    var dfd = $.Deferred();
    var url = "https://ajax.googleapis.com/ajax/services/feed/load?q={0}&v=1.0&callback="
}
// Backbone.Marrionette customizations

Backbone.Marionette.ItemView = Backbone.Marionette.ItemView.extend({
    // add short cut to get/set model properties
    M: function (prop, val) {
        if (!this.model) return;
        if (val == undefined)
            return this.model.get(prop);
        else
            this.model.set(prop, val, { silent: false });
    },
    setButtonEnabled: function (selector, test) {
        var btn = this.$(selector);
        if (btn && btn.length > 0)
            if (test) btn.removeAttr('disabled');
            else btn.attr('disabled', '');
    },
    setItemVisibility: function (selectorOrEl, test) {
        var el;
        if (typeof (selectorOrEl) === 'string')
            el = this.$(selectorOrEl);
        else
            el = selectorOrEl;
        if (el)
            if (test) {
                el.show();
                if (el.css('display') === 'block') el.removeAttr('style');
            } else el.hide();
    },
    setCheckbox: function (prop) {
        if (!prop) return;
        var selector = 'input#' + prop;
        var cb = this.$(selector);
        if (!cb || cb.length == 0) return;
        if (this.M(prop)) cb.attr('checked', '');
        else cb.removeAttr('checked');
    }
});


// use handlebars instead of underscore templates
//Backbone.Marionette.TemplateCache.prototype.compileTemplate = function (rawTemplate) {
//    return Handlebars.compile(rawTemplate);
//};

//Backbone.Marionette.TemplateCache.preloadTemplate = function (templateId, context) {
//    var loader = $.Deferred();
//    var that = this;
//    var msg;
//    var err;
//    if (!templateId || templateId.length == 0) {
//        err = new Error('No templateId was specified - please provide a valid template id or filename.');
//        err.name = "NoTemplateSpecified";
//        throw err;
//    }
//    var hasHasTag = templateId[0] == '#';
//    var template = hasHasTag ? $(templateId).html() : null;
//    if (template && template.length > 0) {
//        Backbone.Marionette.TemplateCache.storeTemplate(templateId, template);
//        loader.resolveWith(context);

//    } else {
//        var fileName = hasHasTag ? templateId.substr(1) : templateId;
//        if (App && App.templateDir)
//            var url = App.templateDir + fileName + '.html';

//        $.get(url, function (serverTemplate) {
//            if (!serverTemplate || serverTemplate.length == 0) {
//                msg = "Could not find template: '" + templateId + "'";
//                err = new Error(msg);
//                err.name = "NoTemplateError";
//                throw err;
//            }

//            Backbone.Marionette.TemplateCache.storeTemplate(templateId, serverTemplate);
//            loader.resolveWith(context);

//        });
//        return loader;
//    }


//};

//Backbone.Marionette.TemplateCache.preloadTemplates = function (templateIds, context) {
//    var loadAllTemplates = $.Deferred();
//    var loadTemplatePromises = [];
//    var that = this;
//    _.each(templateIds, function (templateId, index) {
//        loadTemplatePromises[index] = Backbone.Marionette.TemplateCache.preloadTemplate(templateIds[index], that);
//    });
//    var templatesRemainingToLoad = loadTemplatePromises.length;
//    _.each(loadTemplatePromises, function (aLoadPromise) {
//        $.when(aLoadPromise).done(function () {
//            templatesRemainingToLoad--;
//            if (templatesRemainingToLoad == 0)
//                loadAllTemplates.resolveWith(context); // 'this' context is the module
//        });
//    });
//    return loadAllTemplates;
//};

//Backbone.Marionette.TemplateCache.storeTemplate = function (templateId, template) {
//    // compile template and store in cache
//    template = Backbone.Marionette.TemplateCache.prototype.compileTemplate(template);
//    if (templateId[0] != "#") templateId = "#" + templateId;
//    var cachedTemplate = new Backbone.Marionette.TemplateCache(templateId);
//    cachedTemplate.compiledTemplate = template;
//    Backbone.Marionette.TemplateCache.templateCaches[templateId] = cachedTemplate;
//};

JDM.BoundView = Backbone.Marionette.ItemView.extend({
    constructor: function () {
        if (!this.events) this.events = {};
        this.events["change fieldset input[type=text]"] = "fieldChanged";
        this.events["change fieldset input[type=password]"] = "fieldChanged";
        this.events["change fieldset input[type=checkbox]"] = "checkboxChanged";
        this.events["change fieldset textarea"] = "fieldChanged";
        Backbone.Marionette.ItemView.prototype.constructor.apply(this, arguments);
        // watch changes on the model
        this.listenTo(this.model, "change", this.modelChanged, this);
        this.fadeHtmlChanges = true;
    },
    fieldChanged: function (e) {
        if (e.currentTarget.id != "")
            this.M(e.currentTarget.id, e.currentTarget.value);
    },

    checkboxChanged: function (e) {
        if (e.currentTarget.id != "")
            this.M(e.currentTarget.id, e.currentTarget.checked);
    },

    modelChanged: function (m) {
        for (var prop in m.changed) {
            if (prop === "") continue;
            var el = this.$("#" + prop);
            var newVal = m.changed[prop];
            if (el && el.length > 0) {
                this.updateElement(el, newVal);
            }
        }
        if (this.onChange) this.onChange(m);
    },
    updateElement: function (el, newVal) {
        if (el.is('input'))
            el.val(newVal);
        else {
            if (this.fadeHtmlChanges) {
                el.fadeOut(250, '', function (x) {
                    el.html(newVal);
                    el.fadeIn(250);
                });
            } else {
                el.html(newVal);
            }
        }
    }
});

Backbone.Marionette.Renderer = {

    // Render a template with data. The `template` parameter is
    // passed to the `TemplateCache` object to retrieve the
    // template function. Override this method to provide your own
    // custom rendering and template handling for all of Marionette.
    prefixHtml: "",
    suffixHtml: "",
    render: function (template, data) {
        var templateFunc = typeof template === 'function' ? template : Marionette.TemplateCache.get(template);
        var html = Marionette.Renderer.prefixHtml + templateFunc(data) + Marionette.Renderer.suffixHtml;
        Marionette.Renderer.prefixHtml = "";
        Marionette.Renderer.suffixHtml = "";
        return html;
    }
};

JDM.ModalView = JDM.BoundView.extend({
    constructor: function () {
        if (!this.events) this.events = {};
        this.events["click #cancelButton"] = "defaultCancelRequested";
        this.events["click #actionButton"] = "defaultActionRequested";
        if (!this.ui) this.ui = {};
        this.ui.cancelBtn = "#cancelButton";
        this.ui.actionBtn = "#actionButton";
        this.on('before:render', this.beforeRender, this);
        JDM.BoundView.prototype.constructor.apply(this, arguments);
    },
        
    className: "modal",

    beforeRender: function () {
        if (this.wide) this.$el.addClass("modal-wide");
        Backbone.Marionette.Renderer.prefixHtml = "<div class='modal-header'><button type='button' class='close' data-dismiss='modal'>×</button><h3></h3></div><div class='modal-body'>";
        Backbone.Marionette.Renderer.suffixHtml = "</div><div class='modal-footer'><div  id='cancelButton' class='btn' data-dismiss='modal'>Cancel</div><div id='actionButton' class='btn btn-warning'>Save changes</div></div>";
    },

    onChange: function (m) {
        this.enable();
    },

    onShow: function () {
        this.$el.parent().modal({ show: true });
        if (this.afterShow) this.afterShow();
        this.disable();
    },

    enable: function () {
        this.ui.actionBtn.removeClass("disabled");
        this.actionDisabled = false;
    },

    disable: function () {
        this.ui.actionBtn.addClass("disabled");
        this.actionDisabled = true;
    },

    defaultActionRequested: function () {
        if (this.actionDisabled) return;
        if (this.actionRequested) this.actionRequested();
        else this.defaultAction();
    },

    defaultAction: function () {
        // default action is to save the associated model
        this.listenTo(this.model, 'sync', this.saved, this);
        this.listenTo(this.model, 'error', this.saveFailed, this);
        this.model.save();
    },

    defaultCancelRequested: function () {
        if (this.cancelRequested) this.cancelRequested();
    },


    saved: function () {
        this.model.off('sync', this.saved, this);
        if (this.onSaved) this.onSaved();
        this.close();
    },

    saveFailed: function (model, response) {
        if (this.onSaveFailed) this.onSaveFailed(response.responseText);
    },

    close: function () {
        if (this.onClose) this.onClose();
        this.$el.parent().modal("hide");
    }
});

JDM.ConfirmationView = Marionette.ItemView.extend({
    template: "#confirm",

    constructor: function (options) {
        Backbone.Marionette.ItemView.prototype.constructor.apply(this, arguments);
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

    onShow:function () {
        this.$el.parent().show();
    },
    
    cancelRequested: function () {
        this.close();
        this.target.stopListening(this,'confirmed');
    },

    actionRequested: function () {
        this.trigger('confirmed', this.identifier);
        this.target.stopListening(this,'confirmed');
        this.close();
    }
});

JDM.PartialView = Backbone.Marionette.Layout.extend({
    render: function () {
        //noop
        if (this.onRender) {
            this.onRender();
        }
        return this;
    },
    loadHtml: function (parms, obj) {
        if (!this.url) return;
        $.ajax({
            type: 'get',
            url: this.url,
            data: parms,
            context: this,
            success: function (result) {
                this.$el.html(result);
                this.render();
                obj && obj.loadHtmlCompleted && obj.loadHtmlCompleted();
            }
        });
    },
    setHtml: function (html) {
        this.$el.html(html);
    },
    onShow: function () {
        if (this.url)
            this.loadHtml();
        // make sure events are ready
        this.delegateEvents();
    }
});

JDM.findById = function (arr, id) {
    if (typeof (id) === 'string') id = parseInt(id);
    var foundItem = undefined;
    for (var i = 0; i < arr.length; i++) {
        var item = arr[i];
        if (item.Id === id) {
            foundItem = item;
            break;
        }
    }
    return foundItem;
};

String.format = function () {
    var s = arguments[0];
    for (var i = 0; i < arguments.length - 1; i++) {
        var reg = new RegExp("\\{" + i + "\\}", "gm");
        s = s.replace(reg, arguments[i + 1]);
    }

    return s;
};

String.prototype.pad = function(len, c) {
    var s = this, c = c || '0';
    while (s.length < len) s = c + s;
    return s;
};

// Handlebars - helpers
Handlebars.registerHelper('list', function (context, block) {
    var ret = "<ul>";

    for (var i = 0, j = context.length; i < j; i++) {
        ret = ret + "<li>" + block(context[i]) + "</li>";
    }

    return ret + "</ul>";
});

Handlebars.registerHelper('dropdown', function (context, options) {
    var attrs = '';
    var keys = _.keys(options.hash);
    _.map(keys, function (key) {
        attrs += key + '="' + options.hash[key] + '"';
    });

    var ret = '<ul class="dropdown-menu" ' + attrs + '>';

    if (context.propertyIsEnumerable())
        _.each(context, function (item) {
            ret += "<li>" + options.fn(item) + "</li>";
        })
    else {
        for (var id in context) {
            var obj = { PartitionId: id, Name: context[id] };
            ret += "<li>" + options.fn(obj) + "</li>";
        }
    }


    return ret + "</ul>";
});


Handlebars.registerHelper('picker', function (context, options) {
    var attrs = '';
    var keys = _.keys(options.hash);
    _.map(keys, function (key) {
        attrs += key + '="' + options.hash[key] + '"';
    });

    var label = options.hash["label"] || "Select";
    var ret = '<div class="btn-group dropdown"><a href="#" data-toggle="dropdown" class="btn dropdown-toggle">' + label;
    ret+='<span class="caret"></span></a><ul class="dropdown-menu" role="menu" ' + attrs + '>';

    if (context.propertyIsEnumerable())
        _.each(context, function(item) {
            ret += "<li>" + options.fn(item) + "</li>";
        })
    else {
        for (var index in context) {
            var obj = context[index];
            ret += "<li>" + options.fn(obj) + "</li>";
        }
    }


    return ret + "</ul></div>";
});

Handlebars.registerHelper('pick-items', function (items, options) {
    var ret = "";
    if (!items) items = Handlebars.currentView.getItems();
    for (var index in items) {
        var obj = items[index];
        ret += "<li>" + options.fn(obj) + "</li>";
    }

    return ret;
});

Handlebars.registerHelper('pick2-items', function (items, options) {
    var ret = "";
    if (!items) items = Handlebars.currentView.getItems();
    for (var index in items) {
        var obj = items[index];
        ret += "<option>" + options.fn(obj) + "</option>";
    }

    return ret;
});

Handlebars.registerHelper('tag-pills', function (context, block) {
    var html = "<div class='cj-tags'>Tags:<div class='btn-group'  style='height:30px;'>";

    for (var i = 0, j = context.length; i < j; i++) {
        html += "<a class='btn btn-mini btn-info'>" + block(context[i]) + "</a>";
    }
    return html + "</div></div>";
});

Handlebars.registerHelper('properties', function (context, block) {
    var keys = _.keys(context);
    
    var html = "";
    for (var j = 0; j < keys.length; j++) {
        html += "<div class='cj-tags'>" + keys[j] + ":&nbsp;" + context[keys[j]] + "</div>";        
    }

    return html;
});


