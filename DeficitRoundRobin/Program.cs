using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace DeficitRoundRobin
{
    class Program
    {
        private const int MIN_MESSAGE_SIZE = 8192;
        private const int MESSAGE_SIZE_STEP = 1024;
        private static int MESSAGE_SIZE = 8192;

        private static int MIN_CONSUME_SPEED = 0;
        private static int CONSUME_SPEED = 1;
        private static int CONSUME_SPEED_STEP = 1;

        private static int MIN_HIGH_PRODUCE_SPEED = 0;
        private static int HIGH_PRODUCE_SPEED = 1;
        private static int HIGH_PRODUCE_SPEED_STEP = 1;

        private static int MIN_NORM_PRODUCE_SPEED = 0;
        private static int NORM_PRODUCE_SPEED = 1;
        private static int NORM_PRODUCE_SPEED_STEP = 1;

        private static int MIN_LOW_PRODUCE_SPEED = 0;
        private static int LOW_PRODUCE_SPEED = 1;
        private static int LOW_PRODUCE_SPEED_STEP = 1;

        private const int MENU_LINE_NO = 5;
        private static int RESULT_BASE_LINE_NO { get { return MENU_LINE_NO + Menu.Count + 1; } }

        private static readonly Queue<IMessage> highPriorityQueue = new Queue<IMessage>();
        private static readonly Queue<IMessage> normalPriorityQueue = new Queue<IMessage>();
        private static readonly Queue<IMessage> lowPriorityQueue = new Queue<IMessage>();
        private static readonly Queue<IMessage>[] Queues = new Queue<IMessage>[]
        {
            highPriorityQueue,
            normalPriorityQueue,
            lowPriorityQueue,
        };


        private static long highPriorityMessageSent;
        private static long normalPriorityMessageSent;
        private static long lowPriorityMessageSent;
        private static long totalMessageSent;

        private static int currentParameterItemIndex = 0;
        private static List<IMenuItem> Menu = new List<IMenuItem>
        {
            new ChangeParameterMenuItem("Скорость потребления сообщений",
                 () => CONSUME_SPEED.ToString(),
                reduceHandler: () =>
                {
                    CONSUME_SPEED = CONSUME_SPEED - CONSUME_SPEED_STEP < MIN_CONSUME_SPEED ? CONSUME_SPEED : CONSUME_SPEED - CONSUME_SPEED_STEP;
                },
                increaseHandler: () =>
                {
                    CONSUME_SPEED += CONSUME_SPEED_STEP;
                }),

            new ChangeParameterMenuItem("Скорость продуцирования HIGH",
                () => HIGH_PRODUCE_SPEED.ToString(),
                reduceHandler: () =>
                {
                    HIGH_PRODUCE_SPEED = HIGH_PRODUCE_SPEED - HIGH_PRODUCE_SPEED_STEP < MIN_HIGH_PRODUCE_SPEED ? HIGH_PRODUCE_SPEED : HIGH_PRODUCE_SPEED - HIGH_PRODUCE_SPEED_STEP;
                },
                increaseHandler: () =>
                {
                    HIGH_PRODUCE_SPEED += HIGH_PRODUCE_SPEED_STEP;
                }),

             new ChangeParameterMenuItem("Скорость продуцирования NORM",
                () => NORM_PRODUCE_SPEED.ToString(),
                reduceHandler: () =>
                {
                    NORM_PRODUCE_SPEED = NORM_PRODUCE_SPEED - NORM_PRODUCE_SPEED_STEP < MIN_NORM_PRODUCE_SPEED ? NORM_PRODUCE_SPEED : NORM_PRODUCE_SPEED - NORM_PRODUCE_SPEED_STEP;
                },
                increaseHandler: () =>
                {
                    NORM_PRODUCE_SPEED += NORM_PRODUCE_SPEED_STEP;
                }),

              new ChangeParameterMenuItem("Скорость продуцирования  LOW",
                () => LOW_PRODUCE_SPEED.ToString(),
                reduceHandler: () =>
                {
                    LOW_PRODUCE_SPEED = LOW_PRODUCE_SPEED - LOW_PRODUCE_SPEED_STEP < MIN_LOW_PRODUCE_SPEED ? LOW_PRODUCE_SPEED : LOW_PRODUCE_SPEED - LOW_PRODUCE_SPEED_STEP;
                },
                increaseHandler: () =>
                {
                    LOW_PRODUCE_SPEED += LOW_PRODUCE_SPEED_STEP;
                }),

            new ChangeParameterMenuItem("Размер пакета",
                () => MESSAGE_SIZE.ToString(),
                reduceHandler: () =>
                {
                    MESSAGE_SIZE = MESSAGE_SIZE - MESSAGE_SIZE_STEP < MIN_MESSAGE_SIZE ? MESSAGE_SIZE : MESSAGE_SIZE - MESSAGE_SIZE_STEP;
                },
                increaseHandler: () =>
                {
                     MESSAGE_SIZE += MESSAGE_SIZE_STEP;
                }),

            new ActionMenuItem("Сбросить счетчики приема",
                executeHandler: () =>
                {
                     highPriorityMessageSent = normalPriorityMessageSent = lowPriorityMessageSent = totalMessageSent =0;
                }),
        };

        class ChangeParameterMenuItem: IChangeParameterMenuItem
        {
            private string _caption;
            private Func<string> _valueProvider;
            public Action _reduceHandler;
            public Action _increaseHandler;

            public string Caption { get { return _caption + ": " + _valueProvider.Invoke(); } } 

            public ChangeParameterMenuItem(string caption, Func<string> valueProvider, Action reduceHandler, Action increaseHandler)
            {
                _caption = caption;
                _valueProvider = valueProvider;
                _reduceHandler = reduceHandler;
                _increaseHandler = increaseHandler;
            }

            public void Reduce()
            {
                _reduceHandler.Invoke();
            }

            public void Increase()
            {
                _increaseHandler.Invoke();
            }
        }

        class ActionMenuItem : IActionMenuItem
        {
            private readonly Action _executeHandler;

            public string Caption { get; private set; }

            public ActionMenuItem(string caption, Action executeHandler)
            {
                Caption = caption;
                _executeHandler = executeHandler;
            }

            public void Execute()
            {
                _executeHandler.Invoke();
            }
        }

        interface IMenuItem
        {
            string Caption { get; }
        }

        interface IChangeParameterMenuItem: IMenuItem
        {
            void Reduce();

            void Increase();
        }

        interface IActionMenuItem: IMenuItem
        {
            void Execute();
        }

        class SimpleMenuItem
        {

        }

        static void Main(string[] args)
        {
            Console.CursorVisible = false;
            Console.WriteLine("Для отстановки нажмите любую клавишу ESC");
            Console.WriteLine("Для выбора пункта меню используйте клавиши ↑ и ↓");
            Console.WriteLine("Для изменения параметра используйте клавиши ←, →, PageUp и PageDown");
            Console.WriteLine("Для вызова действия в меню используйте ENTER");

            RunDisplayUpdateThread();
            
            using (stopEvent = new ManualResetEvent(false))
            using (producerStoppedEvent = new ManualResetEvent(false))
            using (consumerStoppedEvent = new ManualResetEvent(false))
            {
                RunProducerThread();
                RunDeficitRoundRobinThread();
                ConsoleKey key;
                while ((key = Console.ReadKey(true).Key) != ConsoleKey.Escape)
                {
                    switch (key)
                    {
                        case ConsoleKey.UpArrow:
                            currentParameterItemIndex = currentParameterItemIndex - 1 < 0 ? Menu.Count - 1 : currentParameterItemIndex - 1;
                            break;
                        case ConsoleKey.DownArrow:
                            currentParameterItemIndex = (currentParameterItemIndex + 1) % Menu.Count;
                            break;
                        case ConsoleKey.Enter:
                            {
                                if(Menu[currentParameterItemIndex] is IActionMenuItem)
                                {
                                    ((IActionMenuItem)Menu[currentParameterItemIndex]).Execute();
                                }
                            }
                            break;
                        case ConsoleKey.PageDown:
                            if (Menu[currentParameterItemIndex] is IChangeParameterMenuItem)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    ((IChangeParameterMenuItem)Menu[currentParameterItemIndex]).Reduce();
                                }
                            }
                            break;
                        case ConsoleKey.LeftArrow:
                            if (Menu[currentParameterItemIndex] is IChangeParameterMenuItem)
                            {
                                ((IChangeParameterMenuItem)Menu[currentParameterItemIndex]).Reduce();
                            }
                            break;
                        case ConsoleKey.PageUp:
                            if (Menu[currentParameterItemIndex] is IChangeParameterMenuItem)
                            {
                                for (int i = 0; i < 10; i++)
                                {
                                    ((IChangeParameterMenuItem)Menu[currentParameterItemIndex]).Increase();
                                }
                            }
                            break;
                        case ConsoleKey.RightArrow:
                            if (Menu[currentParameterItemIndex] is IChangeParameterMenuItem)
                            {
                                ((IChangeParameterMenuItem)Menu[currentParameterItemIndex]).Increase();
                            }
                            break;
                        default:
                            break;
                    }
                }

                stopEvent.Set();
                WaitHandle.WaitAll(new[] { producerStoppedEvent, consumerStoppedEvent });
            }

            Console.ReadKey(true);
        }

        private static void RunDeficitRoundRobinThread()
        {
            ThreadPool.QueueUserWorkItem(z =>
            {
                while (!stopEvent.WaitOne(10)) 
                {
                    lock (sync)
                    {
                        ProcessQueues();
                    }
                }
                consumerStoppedEvent.Set();
            });
        }

        private static void RunProducerThread()
        {
            ThreadPool.QueueUserWorkItem(z =>
            {
                do
                {
                    lock (sync)
                    {

                        for (int i = 0; i < HIGH_PRODUCE_SPEED; i++)
                            highPriorityQueue.Enqueue(new Msg(MESSAGE_SIZE));
                        for (int i = 0; i < NORM_PRODUCE_SPEED; i++)
                            normalPriorityQueue.Enqueue(new Msg(MESSAGE_SIZE));
                        for (int i = 0; i < LOW_PRODUCE_SPEED; i++)
                            lowPriorityQueue.Enqueue(new Msg(MESSAGE_SIZE));
                    }
                } while (!stopEvent.WaitOne(10));
                producerStoppedEvent.Set();
            });
        }
        private readonly static object sync = new object();
        private static void RunDisplayUpdateThread()
        {
            ThreadPool.QueueUserWorkItem(z =>
            {
                while (true)
                {
                    lock (sync)
                    {
                        UpdateDisplay();
                    }
                    Thread.Sleep(75);
                }
            });
        }

        private static void UpdateDisplay()
        {
            UpdateMenu();

            UpdateHighPriorityQueueState();
            UpdateNormalPriorityQueueState();
            UpdateLowPriorityQueueState();

            UpdateHighPriorityMessageSentState();
            UpdateNormalPriorityMessageSentState();
            UpdateLowPriorityMessageSentState();
        }


        private static int n = Queues.Length;
        private static int[] dc = new int[n];
        private static ManualResetEvent stopEvent;
        private static ManualResetEvent producerStoppedEvent;
        private static ManualResetEvent consumerStoppedEvent;

        private static void ProcessQueues()
        {
            var Q = (int)Math.Ceiling(((double)CONSUME_SPEED / ((0.7 / 0.1) + (0.2 / 0.1) + 1)) * MESSAGE_SIZE);
            var q = new int[] { (int)Math.Ceiling(Q * (0.7 / 0.1)), (int)Math.Ceiling(Q * (0.2 / 0.1)), Q };
        
            //Deficit round robin
            for (int i = 0; i < n; i++)
            {
                if(Queues[i].Any())
                {
                    dc[i] = dc[i] + q[i];

                    while (Queues[i].Any() && dc[i] >= Queues[i].Peek().Size)
                    {
                        var msg = Queues[i].Dequeue();
                        dc[i] = dc[i] - msg.Size;
                        Send(i, msg);
                    }
                    if (!Queues[i].Any())
                    {
                        dc[i] = 0;
                    }
                }
            }
        }

        private static void Send(int queueIndex, IMessage msg)
        {
            totalMessageSent += msg.Size;

            switch (queueIndex)
            {
                case 0:
                    highPriorityMessageSent += msg.Size;
                    break;
                case 1:
                    normalPriorityMessageSent += msg.Size;
                    break;
                case 2:
                    lowPriorityMessageSent += msg.Size;
                    break;
                default:
                    break;
            }
        }

        private static void UpdateMenu()
        {
            var left = Console.CursorLeft;
            var top = Console.CursorTop;
            try
            {
                var tmp = currentParameterItemIndex;
                Console.SetCursorPosition(0, MENU_LINE_NO);
                for (int i = 0; i < Menu.Count; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                    Console.SetCursorPosition(0, MENU_LINE_NO + i);
                    if (i == tmp)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.Gray;
                    }
                    
                    Console.WriteLine(Menu[i].Caption);

                    if (i == tmp)
                    {
                        Console.ResetColor();
                    }
                }
            }
            finally
            {
                Console.SetCursorPosition(left, top);
            }
        }

        private static void UpdateHighPriorityQueueState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO;
            const ConsoleColor BG_COLOR = ConsoleColor.Red;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;

            UpdateQueueState("HIGH: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, highPriorityQueue.Count);
        }

        private static void UpdateNormalPriorityQueueState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO + 3;
            const ConsoleColor BG_COLOR = ConsoleColor.Yellow;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;

            UpdateQueueState("NORM: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, normalPriorityQueue.Count);
        }
        
        private static void UpdateLowPriorityQueueState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO + 6;
            const ConsoleColor BG_COLOR = ConsoleColor.White;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;
            UpdateQueueState(" LOW: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, lowPriorityQueue.Count);
        }

        private static void UpdateHighPriorityMessageSentState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO + 10;
            const ConsoleColor BG_COLOR = ConsoleColor.Red;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;

            UpdateMessageSentState("HIGH: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, highPriorityMessageSent);
        }

        private static void UpdateNormalPriorityMessageSentState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO + 11;
            const ConsoleColor BG_COLOR = ConsoleColor.Yellow;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;

            UpdateMessageSentState("NORM: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, normalPriorityMessageSent);
        }

        private static void UpdateLowPriorityMessageSentState()
        {
            int CONSOLE_LINE_NO = RESULT_BASE_LINE_NO + 12;
            const ConsoleColor BG_COLOR = ConsoleColor.White;
            const ConsoleColor FG_COLOR = ConsoleColor.Black;

            UpdateMessageSentState(" LOW: ", CONSOLE_LINE_NO, BG_COLOR, FG_COLOR, lowPriorityMessageSent);
        }

       
        private static void UpdateMessageSentState(string caption, int lineNo, ConsoleColor bgColor, ConsoleColor fgColor, long sent)
        {
            var left = Console.CursorLeft;
            var top = Console.CursorTop;
            try
            {
                Console.SetCursorPosition(0, lineNo);

                Console.ForegroundColor = fgColor;
                Console.BackgroundColor = bgColor;

                var sentAsStr = sent.ToString();
                sentAsStr = new string(' ', 10 - sentAsStr.Length > 0 ? 10 - sentAsStr.Length : 0) + sentAsStr;
                Console.WriteLine("{0}{1}({2} %)                        ", caption, sentAsStr, ((double)sent * 100.0 / totalMessageSent).ToString("N1"));
            }
            finally
            {
                Console.ResetColor();
                Console.SetCursorPosition(left, top);
            }
        }

        private static void UpdateQueueState(string caption, int lineNo, ConsoleColor bgColor, ConsoleColor fgColor, int count)
        {
            var left = Console.CursorLeft;
            var top = Console.CursorTop;
            try
            {
                Console.SetCursorPosition(0, lineNo);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, lineNo);

                Console.ForegroundColor = fgColor;
                Console.BackgroundColor = bgColor;
                var countAsStr = count.ToString();
                countAsStr = new string(' ', 5 - countAsStr.Length > 0 ? 5 - countAsStr.Length : 0) + countAsStr;

                var markerCount = (int)Math.Ceiling((double)count / 100);
                markerCount = markerCount < (Console.WindowWidth * 2 + (Console.WindowWidth - caption.Length - countAsStr.Length)) 
                    ? markerCount 
                    : (Console.WindowWidth * 2 + (Console.WindowWidth - caption.Length - countAsStr.Length));
                Console.WriteLine("{0}{1}{2}", caption, countAsStr, new string('■', markerCount));
            }
            finally
            {
                Console.ResetColor();
                Console.SetCursorPosition(left, top);
            }
        }
    }

    interface IMessage
    {
        int Size { get; }
    }

    class Msg : IMessage
    {
        public int Size { get; private set; }

        public Msg(int size)
        {
            Size = size;
        }
    }

}
