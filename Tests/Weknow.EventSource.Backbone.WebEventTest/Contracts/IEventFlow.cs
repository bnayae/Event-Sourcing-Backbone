﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Weknow.EventSource.Backbone.WebEventTest
{
    [GenerateEventSource(EventSourceGenType.Consumer)]
    [GenerateEventSource(EventSourceGenType.Producer)]
    //[GenerateEventSource(EventSourceGenType.Consumer, Namespace = "Weknow.EventSource.Backbone.WebEventTest")]
    //[GenerateEventSource(EventSourceGenType.Producer, Namespace = "Weknow.EventSource.Backbone.WebEventTest")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Used for code generation, use the producer / consumer version of it", true)]
    public interface IEventFlow
    {
        /// <summary>
        /// Stages 1.
        /// </summary>
        /// <param name="PII">The PII.</param>
        /// <param name="payload">The payload.</param>
        /// <returns></returns>
        ValueTask Stage1Async(Person PII, string payload);
        /// <summary>
        /// Stages the 2.
        /// </summary>
        /// <param name="PII">The PII.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        ValueTask Stage2Async(JsonElement PII, JsonElement data);
    }
}
