using System.Threading.Tasks;

namespace GPS.SimpleThreading.Blocks
{
    public sealed partial class ThreadBlock<TDataItem, TResult>
    {
        public class DataResultPair
        {
            public Task Task {get; private set; }
            public TDataItem Data { get; private set; }
            public TResult Result { get; private set; }

            public DataResultPair(Task task, TDataItem data, TResult result)
            {
                Task = task;
                Data = data;
                Result = result;
            }
        }
    }
}
