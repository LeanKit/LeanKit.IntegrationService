//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using Ploeh.AutoFixture;

namespace IntegrationService.Tests
{
    public abstract class SpecBase
    {
        public SpecBase()
        {
            Configure();
        }
        protected virtual void OnStartFixture() { }
        protected virtual void OnConfigureObjectFactory() { }
        protected virtual void OnCreateMockObjects(){ }
        protected virtual void OnStartService() { }
        protected virtual void OnArrange() { }
        protected virtual void OnSetExpectations() { }
        protected virtual void OnStartTest() { }

        protected virtual void Configure()
        {
            OnStartFixture();
            //ObjectFactory.Initialize(c => { });
            OnConfigureObjectFactory();
            OnCreateMockObjects();
            OnStartService();
            OnArrange();
            OnSetExpectations();
            OnStartTest();
        }

    }

    public static class Test<T>
    {
        public static T Item
        {
            get { return new Fixture().Create<T>(); }
        }
    }

}