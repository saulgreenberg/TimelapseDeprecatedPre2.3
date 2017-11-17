using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Timelapse.Util
{
    // Utilities to support parallelism
    // Note that we currently  use only some of the methods below, as we have turned off parallelism when loading files.
    // However, as we may enable that at a future time, we have kept all methods.
    internal class SequentialPartitioner<TSource> : OrderablePartitioner<TSource>
    {
        private IList<TSource> source;

        public override bool SupportsDynamicPartitions
        {
            get { return true; }
        }

        public SequentialPartitioner(IList<TSource> source)
            : base(true, false, false)
        {
            this.source = source;
        }

        public override IEnumerable<KeyValuePair<long, TSource>> GetOrderableDynamicPartitions()
        {
            return new DynamicPartitions(this.source);
        }

        public override IList<IEnumerator<KeyValuePair<long, TSource>>> GetOrderablePartitions(int partitionCount)
        {
            List<IEnumerator<KeyValuePair<long, TSource>>> partitions = new List<IEnumerator<KeyValuePair<long, TSource>>>(partitionCount);
            for (int partition = 0; partition < partitionCount; ++partition)
            {
                partitions.Add(new InterleavedOrderableEnumerator(this.source, partition, partitionCount));
            }
            return partitions;
        }

        public override IList<IEnumerator<TSource>> GetPartitions(int partitionCount)
        {
            List<IEnumerator<TSource>> partitions = new List<IEnumerator<TSource>>(partitionCount);
            for (int partition = 0; partition < partitionCount; ++partition)
            {
                partitions.Add(new InterleavedEnumerator(this.source, partition, partitionCount));
            }
            return partitions;
        }

        private class DynamicPartitions : IEnumerable<KeyValuePair<long, TSource>>
        {
            private int currentIndex;
            private IList<TSource> source;

            internal DynamicPartitions(IList<TSource> source)
            {
                this.currentIndex = 0;
                this.source = source;
            }

            public IEnumerator<KeyValuePair<long, TSource>> GetEnumerator()
            {
                while (true)
                {
                    int index = Interlocked.Increment(ref this.currentIndex) - 1;
                    if (index < this.source.Count)
                    {
                        yield return new KeyValuePair<long, TSource>(index, this.source[index]);
                    }
                    else
                    {
                        yield break;
                    }
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        private class InterleavedEnumerator : IEnumerator<TSource>
        {
            private int currentIndex;
            private int offset;
            private IList<TSource> source;
            private int stride;

            object IEnumerator.Current
            {
                get { return this.Current; }
            }

            public TSource Current { get; private set; }

            public InterleavedEnumerator(IList<TSource> source, int offset, int stride)
            {
                this.currentIndex = offset;
                this.offset = offset;
                this.source = source;
                this.stride = stride;
            }

            public void Dispose()
            {
                // nothing to do
            }

            public bool MoveNext()
            {
                this.currentIndex += this.stride;
                if (this.currentIndex < this.source.Count)
                {
                    this.Current = this.source[this.currentIndex];
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                this.currentIndex = this.offset;
            }
        }

        private class InterleavedOrderableEnumerator : IEnumerator<KeyValuePair<long, TSource>>
        {
            private int currentIndex;
            private int offset;
            private IList<TSource> source;
            private int stride;

            object IEnumerator.Current
            {
                get { return this.Current; }
            }

            public KeyValuePair<long, TSource> Current { get; private set; }

            public InterleavedOrderableEnumerator(IList<TSource> source, int offset, int stride)
            {
                this.currentIndex = offset;
                this.offset = offset;
                this.source = source;
                this.stride = stride;
            }

            public void Dispose()
            {
                // nothing to do
            }

            public bool MoveNext()
            {
                this.currentIndex += this.stride;
                if (this.currentIndex < this.source.Count)
                {
                    this.Current = new KeyValuePair<long, TSource>(this.currentIndex, this.source[this.currentIndex]);
                    return true;
                }
                return false;
            }

            public void Reset()
            {
                this.currentIndex = this.offset;
            }
        }
    }
}
