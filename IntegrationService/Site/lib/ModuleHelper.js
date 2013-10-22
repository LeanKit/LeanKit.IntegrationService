


// use handlebars instead of underscore templates
Marionette.TemplateCache.prototype.compileTemplate = function (rawTemplate) {
    return Handlebars.compile(rawTemplate);
};

// workaround for a handlebars issue
// compiled templates get an extra text node generated
// due to an apparent error. For more info:https://github.com/wycats/handlebars.js/issues/508
// this rewrites Marionette's render method to strip off the offending character before
// the template is used.
Marionette.ItemView = Marionette.ItemView.extend({
    render: function () {
        this.isClosed = false;

        this.triggerMethod("before:render", this);
        this.triggerMethod("item:before:render", this);

        var data = this.serializeData();
        data = this.mixinTemplateHelpers(data);

        var template = this.getTemplate();
        var html = Marionette.Renderer.render(template, data);
        // fix handlebars template
        if (html.indexOf("<") === 1)
            html = html.substr(1);

        this.$el.html(html);
        this.bindUIElements();

        this.triggerMethod("render", this);
        this.triggerMethod("item:rendered", this);

        return this;
    }
});


Marionette.ModuleHelper = (function (Marionette, _) {
    "use strict";

    var moduleHelper = {};

    moduleHelper.templatePath = 'templates/';
    moduleHelper.templateExt = ".html";
    moduleHelper.templatePrefix = "";

    moduleHelper.loadModuleTemplates = function (module, callback, usePrecompiledTemplates) {
        moduleHelper.templatePath = module.templatePath;
        moduleHelper.templatePrefix = module.prefix;

        if (usePrecompiledTemplates) {
            // store precompiled templates as templateCaches and go
            moduleHelper.storePrecompiledTemplates(Handlebars[moduleHelper.templatePrefix]);
            module.triggerMethod("templates:loaded", this);
        } else {
            var templatesToLoad = [];
            for (var viewName in module.views) {
                var view = module.views[viewName];
                // skip if undefined
                if (_.isObject(view.prototype) && _.isString(view.prototype.template))
                    templatesToLoad.push(view.prototype.template);
            }
            var loadingTemplates = moduleHelper.preloadTemplates(templatesToLoad, module);
            $.when(loadingTemplates).done(callback);
        }
    };


    moduleHelper.preloadTemplate = function (templateId, context) {
        var loader = $.Deferred();
        var that = this;
        var msg;
        var err;
        if (!templateId || templateId.length == 0) {
            err = new Error('No templateId was specified - please provide a valid template id or filename.');
            err.name = "NoTemplateSpecified";
            throw err;
        }
        var hasHasTag = templateId.substr(0, 1) === '#';
        var template = hasHasTag ? $(templateId).html() : null;
        if (template && template.length > 0) {
            moduleHelper.storeTemplate(templateId, template);
            loader.resolveWith(context);

        } else {
            var dashPos = templateId.indexOf('-');
            var fileName = hasHasTag
                ? templateId.substr(1)
                : dashPos > 0
                    ? templateId.substr(dashPos + 1)
                    : templateId;
            var url = moduleHelper.templatePath + fileName + moduleHelper.templateExt;

            $.get(url, function (serverTemplate) {
                if (!serverTemplate || serverTemplate.length == 0) {
                    msg = "Could not find template: '" + templateId + "'";
                    err = new Error(msg);
                    err.name = "NoTemplateError";
                    throw err;
                }

                // templateId = (context.templatePrefix) ? context.templatePrefix + templateId : templateId;
                moduleHelper.storeTemplate(templateId, serverTemplate);
                loader.resolveWith(context);

            });
            return loader;
        }
    };

    moduleHelper.preloadTemplates = function (templateIds, context) {
        var loadAllTemplates = $.Deferred();
        var loadTemplatePromises = [];
        var that = this;
        _.each(templateIds, function (templateId, index) {
            loadTemplatePromises[index] = moduleHelper.preloadTemplate(templateIds[index], moduleHelper);
        });
        var templatesRemainingToLoad = loadTemplatePromises.length;
        _.each(loadTemplatePromises, function (aLoadPromise) {
            $.when(aLoadPromise).done(function () {
                templatesRemainingToLoad--;
                if (templatesRemainingToLoad == 0)
                    loadAllTemplates.resolveWith(context); // 'this' context is the module
            });
        });
        return loadAllTemplates;
    };

    moduleHelper.storeTemplate = function (templateId, template) {
        // compile template and store in cache
        template = Marionette.TemplateCache.prototype.compileTemplate(template);
        //if (templateId[0] != "#") templateId = "#" + templateId;
        var cachedTemplate = new Marionette.TemplateCache(templateId);
        cachedTemplate.compiledTemplate = template;
        Marionette.TemplateCache.templateCaches[templateId] = cachedTemplate;
    };

    moduleHelper.storePrecompiledTemplates = function (templates) {
        _.each(_.keys(templates), function (key) {
            var templateId = moduleHelper.templatePrefix + '-' + key;
            var i = templateId.indexOf('.html');
            if (i > 0) {
                templateId = templateId.substr(0, i);
            }
            var cachedTemplate = new Marionette.TemplateCache(templateId);
            cachedTemplate.compiledTemplate = templates[key];
            Marionette.TemplateCache.templateCaches[templateId] = cachedTemplate;
        });
    };

    return moduleHelper;
})(Marionette, _);




