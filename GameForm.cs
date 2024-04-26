using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace WindowsFormsApp1
{

    public class Wolf
    {
        public int HP;
        public Point WolfPosition;
        public int[,] SmellField;
        public static int CellSize;
        public static int width;
        public static int height;
        public Wolf(Point RabbitPosition)
        {
            this.HP = 100;
            this.WolfPosition = new Point();
            RefillSmellField(RabbitPosition);
        }
        public void RefillSmellField(Point RabbitPosition)
        {
            this.SmellField = new int[width, height];
            int maxValue = Math.Max(width, height);
            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    int distanceToRabbit = Math.Max(Math.Abs(i - (int)RabbitPosition.X / CellSize), Math.Abs(j - (int)RabbitPosition.Y / CellSize));
                    this.SmellField[i, j] = maxValue - distanceToRabbit;
                }
            }
        }
        public Point GetMaxSmellNeighbor()
        {
            int maxSmell = int.MinValue;
            Point maxSmellPosition = new Point(-1, -1);

            // Просматриваем все соседние клетки
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    int neighborX = (int)this.WolfPosition.X / Wolf.CellSize + i;
                    int neighborY = (int)this.WolfPosition.Y / Wolf.CellSize + j;

                    // Проверяем, что соседняя клетка находится в пределах поля
                    if (neighborX >= 0 && neighborX < width &&
                        neighborY >= 0 && neighborY < height)
                    {
                        // Если запах в соседней клетке больше, чем текущий максимум, обновляем максимум и позицию
                        if (this.SmellField[neighborX, neighborY] > maxSmell)
                        {
                            maxSmell = this.SmellField[neighborX, neighborY];
                            maxSmellPosition = new Point(neighborX * CellSize, neighborY * CellSize);
                        }
                    }
                }
            }

            return maxSmellPosition;
        }

    }

    public partial class GameForm : Form
    {
        private Thread rabbitThread; // Поток кролика
        private Thread carrotThread; // Поток морковок
        private Thread trapThread;  //Поток ловушек
        private Thread wolfThread; //Поток волков
        private readonly object syncLock = new object(); //?

        private Wolf Wolf;

        private Point targetRabbitPosition; // Ключевая позиция кролика
        private const int moveDuration = 50; // Движение за 500 мс
        private DateTime moveStartTime; // Время начала движения
        private DateTime moveStartTimeWolf; // Время начала движения волка
        private Point startPosition; // Начальная позиция кролика
        private bool isMoving = false; // Движится ли кролик


        private bool isGameRunning;  //Идет ли игра
        private CancellationTokenSource cancellationTokenSource; // ?
        private Point rabbitPosition; // Позиция кролика
        private List<Rectangle> carrots;  // Хранит позиции морковок
        private List<Point> traps; // Хранит позиции ловушек

        private const int CellSize = 100; // Размер ячейки
        private const int GridWidth = 10; // Ширина ячейки
        private const int GridHeight = 6; // Высота ячейки

        private Bitmap backgroundBuffer; // Фон глобальный

        private Image rabbitImage; // Изображение кролика
        private Image carrotImage; // Изображение морковки
        private Image cellBackgroundImage; // Фон ячейки
        private Image trapImage; // Изображение ловушек
        private Image wolfImage; // Изображение волков

        private bool gameIsOver = false; // Проиграна ли игра

        public bool WolfFlag = false;
        bool WolfDead = false;
        int WolfTrigger = 100;



        public GameForm()
        {
            try
            {
                InitializeComponent(); // Инициализация компонентов
                this.DoubleBuffered = true;
                this.ClientSize = new Size(CellSize * GridWidth, CellSize * GridHeight); // Размер главной формы
                this.Text = "Собери всю морковку";
                cancellationTokenSource = new CancellationTokenSource();

                rabbitPosition = new Point(100, 100);  // Позиция кролика


                Wolf.height = GridHeight;
                Wolf.width = GridWidth;
                Wolf.CellSize = CellSize;
                LoadResources(); // Загрузка ресурсов
                CreateBackgroundBuffer();
                InitializeLevel(); // Инициализация уровня
                this.KeyDown += new KeyEventHandler(GameForm_KeyDown); // Нажата кнопка в форме}
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Формы игры");
            }
        }



        private void InitializeLevel()
        {
            try
            {
                isGameRunning = true;
                //PAO 26.04.24
                //rabbitPosition = new Point(200, 200);// Смена позиции кролика с 100 100 в 250 200 зачем?

                carrots = new List<Rectangle>(); // Список морквы

                for (int i = 0; i < GridHeight; i++) // Заполнение списока морквы
                {
                    for (int j = 0; j < GridWidth; j++)
                    {
                        if ((i + j) % 2 == 0)
                        {
                            carrots.Add(new Rectangle(j * CellSize, i * CellSize, CellSize, CellSize));
                        }
                    }
                }

                traps = new List<Point>();
                Random random = new Random();
                int maxTrapsCount = 4;// random.Next(4, Math.Max(GridWidth, GridHeight));
                while (traps.Count < maxTrapsCount)
                {
                    Point trapPosition = new Point(random.Next(0, GridWidth) * CellSize, random.Next(0, GridHeight) * CellSize);
                    // Проверка, что ловушка не находится в той же позиции, что и морковка или кролик
                    if (!traps.Contains(trapPosition) &&
                        !carrots.Exists(c => c.Contains(trapPosition)) &&
                        trapPosition != rabbitPosition)
                    {
                        traps.Add(trapPosition);
                    }
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Инициализации");
            }
        }


        private void CreateBackgroundBuffer()  // отрисовка фона
        {
            try
            {
                // Очистка памяти занятым главным фоном
                if (backgroundBuffer != null)
                {
                    backgroundBuffer.Dispose();
                }

                // Создание буфера
                backgroundBuffer = new Bitmap(CellSize * GridWidth, CellSize * GridHeight);
                using (Graphics g = Graphics.FromImage(backgroundBuffer))
                {
                    for (int i = 0; i < GridWidth; i++)
                    {
                        for (int j = 0; j < GridHeight; j++)
                        {
                            // Отрисовка ij ячейки
                            g.DrawImage(cellBackgroundImage, i * CellSize, j * CellSize, CellSize, CellSize);
                        }
                    }
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Фона");
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                base.OnPaint(e);
                Graphics g = e.Graphics;

                // Отрисовка сохраненного фона из буфера
                g.DrawImage(backgroundBuffer, 0, 0);
                foreach (Point trap in traps.ToList())
                {
                    if (e.ClipRectangle.IntersectsWith(new Rectangle(trap.X, trap.Y, CellSize, CellSize)))
                    {
                        DrawTrap(g, trap.X, trap.Y);
                    }
                }

                // Отрисовка морковок и ловушек в области, требующей перерисовки
                foreach (var carrot in carrots.ToList())
                {
                    if (e.ClipRectangle.IntersectsWith(new Rectangle(carrot.X, carrot.Y, CellSize, CellSize)))
                    {
                        DrawCarrot(g, carrot.X, carrot.Y);
                    }
                }

                // Отрисовка кролика, если его позиция пересекается с областью перерисовки
                if (e.ClipRectangle.IntersectsWith(new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize)))
                {
                    DrawRabbit(g, rabbitPosition.X, rabbitPosition.Y);
                }

                // Отрисовка волка, если его позиция пересекается с областью перерисовки
                if (WolfFlag && !WolfDead)
                {
                    if (e.ClipRectangle.IntersectsWith(new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize)))
                    {
                        DrawWolf(g, Wolf.WolfPosition.X, Wolf.WolfPosition.Y);
                    }
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Отрисовки");
            }
        }



        private void DrawTrap(Graphics g, int x, int y) // отрисовка ловушек
        {
            g.DrawImage(trapImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void GameForm_Load(object sender, EventArgs e)
        {
            StartGame();  // начало игры
        }

        private void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                if (!isGameRunning || isMoving) return; // Игнорируем ввод, если игра остановлена или идёт анимация

                var newRabbitPosition = rabbitPosition; // Частичное переопределение позиции
                switch (e.KeyCode)
                {
                    case Keys.Up:
                        newRabbitPosition.Y -= CellSize;
                        break;
                    case Keys.Down:
                        newRabbitPosition.Y += CellSize;
                        break;
                    case Keys.Left:
                        newRabbitPosition.X -= CellSize;
                        break;
                    case Keys.Right:
                        newRabbitPosition.X += CellSize;
                        break;
                    case Keys.W:
                        newRabbitPosition.Y -= CellSize;
                        break;
                    case Keys.S:
                        newRabbitPosition.Y += CellSize;
                        break;
                    case Keys.A:
                        newRabbitPosition.X -= CellSize;
                        break;
                    case Keys.D:
                        newRabbitPosition.X += CellSize;
                        break;
                }

                // Проверка, находится ли новая позиция в пределах игрового поля и нет ли там ловушки
                if (newRabbitPosition.X >= 0 && newRabbitPosition.X < CellSize * GridWidth &&
                newRabbitPosition.Y >= 0 && newRabbitPosition.Y < CellSize * GridHeight)
                {
                    if (!isMoving) // Проверяем, что анимация не выполняется
                    {
                        startPosition = rabbitPosition;
                        targetRabbitPosition = newRabbitPosition;
                        moveStartTime = DateTime.Now;
                        isMoving = true; // Запускаем анимацию
                    }
                }
                if (WolfFlag)
                {

                    /*
                    Rectangle WolfRect = new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize);
                    Rectangle RabbRect = new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize);
                    if (WolfRect.IntersectsWith(RabbRect))
                    {
                        Wolf.WolfPosition = new Point(rabbitPosition.X, rabbitPosition.Y);
                        Invoke((MethodInvoker)GameOver);
                    }
                    Invalidate(WolfRect);

                     */
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Управления");
            }
        }

        private void LoadResources()
        {
            rabbitImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Rabbit.png");
            carrotImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Carrots.png");
            cellBackgroundImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Grass.png");
            trapImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Spikes.png");
            wolfImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Wolf.png");
        }



        private void WinGame()
        {

            isGameRunning = false;
            rabbitThread.Abort();
            carrotThread.Abort();
            trapThread.Abort();
            if (WolfFlag)
            {
                wolfThread.Abort();
            }
            Invoke((MethodInvoker)delegate
            {
                MessageBox.Show("Вы выиграли! Все морковки собраны.");
                Close();
            });


            /*
             try {
             }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Победы");
            }
             */
        }


        private void StartGame()
        {
            try
            {
                isGameRunning = true;

                rabbitThread = new Thread(RabbitMovement) { IsBackground = true };
                carrotThread = new Thread(CheckCarrot) { IsBackground = true };

                rabbitThread.Start();
                carrotThread.Start();

                trapThread = new Thread(TrapBehavior) { IsBackground = true };
                trapThread.Start();
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
            }


        }

        private void TrapBehavior()
        {
            try
            {
                while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
                {
                    lock (syncLock)
                    {
                        foreach (Point trap in traps.ToList())
                        {
                            if (rabbitPosition == trap)
                            {
                                Invoke((MethodInvoker)GameOver);
                                return; // Завершаем поток, так как кролик пойман в ловушку
                            }
                        }
                    }
                    if (WolfFlag)
                    {
                        for (int i = traps.Count - 1; i >= 0; i--)
                        {
                            if (i < traps.Count && traps.Count >= 0)
                            {
                                Rectangle WolfRect = new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize);
                                Rectangle TrapRect = new Rectangle(traps[i].X, traps[i].Y, CellSize, CellSize);
                                if (WolfRect.IntersectsWith(TrapRect))
                                {
                                    Wolf.HP -= 500;
                                    if (Wolf.HP < 0)
                                    {
                                        wolfThread.Abort();
                                        Invalidate(WolfRect);
                                        WolfDead = true;
                                    }
                                    traps.RemoveAt(i);
                                    Thread.Sleep(2000);
                                    Invalidate(TrapRect);
                                    Invalidate(WolfRect);
                                }
                            }
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Ловушки");
            }
        }

        private List<Point> rabbitPositionHistory = new List<Point>();
        private const int maxPositionHistoryLength = 2;


        private void RabbitMovement()
        {
            try
            {
                while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
                {
                    if (isMoving)
                    {
                        Invoke((MethodInvoker)delegate
                        {
                            var currentTime = DateTime.Now;
                            var elapsed = (currentTime - moveStartTime).TotalMilliseconds;
                            if (elapsed < moveDuration)
                            {
                                double t = elapsed / moveDuration;
                                Point oldRabbitPosition = rabbitPosition;
                                rabbitPosition = new Point(
                                    startPosition.X + (int)((targetRabbitPosition.X - startPosition.X) * t),
                                    startPosition.Y + (int)((targetRabbitPosition.Y - startPosition.Y) * t)
                                );
                            }
                            else
                            {
                                rabbitPosition = targetRabbitPosition;
                                isMoving = false;

                            }

                            rabbitPositionHistory.Add(targetRabbitPosition);
                            rabbitPositionHistory.Add(rabbitPosition);

                            // Ограничение длины истории
                            if (rabbitPositionHistory.Count > maxPositionHistoryLength)
                            {
                                rabbitPositionHistory.RemoveAt(0);
                            }
                            foreach (Point point in rabbitPositionHistory.ToList())
                            {
                                Rectangle dirtyRect = new Rectangle(point.X, point.Y, CellSize, CellSize);
                                Invalidate(dirtyRect);
                            }
                            if (WolfFlag)
                            {
                                Wolf.RefillSmellField(rabbitPosition);
                            }
                        });
                        if (WolfFlag && !WolfDead)
                        {
                            Rectangle WolfRect = new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize);
                            Rectangle RabbitRect = new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize);
                            if (WolfRect.IntersectsWith(RabbitRect))
                            {
                                Invalidate(RabbitRect);
                                Invalidate(WolfRect);
                                deadFromWolf = true;
                                Invoke((MethodInvoker)GameOver);
                                return;
                            }
                        }
                        foreach (Point trap in traps.ToList())
                        {
                            if (rabbitPosition == trap)
                            {
                                Invoke((MethodInvoker)GameOver);
                                return; // Завершаем поток, так как кролик пойман в ловушку
                            }
                        }
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Кроля");
            }
        }




        private void CheckCarrot()
        {
            try
            {
                while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        CheckRabbitCarrotCollision();
                    });
                    Thread.Sleep(10); //FV2
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Проверки морковки");
            }
        }

        private async void CheckRabbitCarrotCollision()
        {
            try
            {
                Rectangle rabbitRectangle = new Rectangle(rabbitPosition, new Size(CellSize, CellSize));
                bool eaten = false;

                for (int i = carrots.Count - 1; i >= 0; i--)
                {
                    if (i < carrots.Count && carrots.Count >= 0)
                    {
                        if (rabbitRectangle.IntersectsWith(carrots[i]))
                        {
                            eaten = true;
                            carrots.RemoveAt(i);
                        }
                    }
                }
                if (WolfFlag)
                {
                    for (int i = carrots.Count - 1; i >= 0; i--)
                    {
                        if (i < carrots.Count && carrots.Count >= 0)
                        {
                            Rectangle WolfRect = new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize);
                            if (WolfRect.IntersectsWith(carrots[i]))
                            {
                                this.MoveDurationWolf += 500;
                                if (MoveDurationWolf > 2000)
                                {
                                    wolfThread.Abort();
                                    Invalidate(WolfRect);
                                    WolfDead = true;
                                }
                            }
                        }
                    }
                }
                if (eaten)
                {
                    if (carrots.Count == 0)
                    {
                        await Task.Delay(150);
                        WinGame();
                    }
                    if (carrots.Count < WolfTrigger && !WolfFlag || carrots.Count < WolfTrigger && WolfFlag && WolfDead)//|| carrots.Count < 5 && WolfDead && WolfFlag
                    {
                        bool SpawnFlag = true;
                        Point WolfPoint = new Point(random.Next(0, GridWidth) * CellSize, random.Next(0, GridHeight) * CellSize);
                        while (SpawnFlag)
                        {
                            Rectangle WolfPo = new Rectangle(WolfPoint.X, WolfPoint.Y, CellSize, CellSize);
                            bool lockalFlag = true;
                            foreach (Point trap in traps.ToList())
                            {
                                Rectangle tr = new Rectangle(trap.X, trap.Y, CellSize, CellSize);
                                if (tr.IntersectsWith(WolfPo))
                                {
                                    lockalFlag = false;
                                    break;
                                }
                            }
                            if (lockalFlag)
                            {
                                Rectangle RabP = new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize);
                                if (!RabP.IntersectsWith(WolfPo))
                                {
                                    SpawnFlag = false;
                                    continue;
                                }
                            }
                            WolfPoint = new Point(random.Next(0, GridWidth) * CellSize, random.Next(0, GridHeight) * CellSize);
                        }
                        Wolf = new Wolf(WolfPoint);
                        WolfFlag = true;
                        WolfDead = false;
                        MoveDurationWolf = (int)(MoveDurationWolf * 0.7);
                        wolfThread = new Thread(WolfAction) { IsBackground = true };
                        wolfThread.Start();
                    }
                }
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Морковки");
            }
        }
        private static Random random = new Random();
        private List<Point> wolfPositionHistory = new List<Point>();
        private int MoveDurationWolf = 1000;
        private async void WolfAction()
        {

            while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
            {
                var MoveStart = DateTime.Now;

                Invoke((MethodInvoker)delegate
                {
                    Point nextStuff = Wolf.GetMaxSmellNeighbor();
                    var currentTime = DateTime.Now;

                    var elapsed = (currentTime - MoveStart).TotalMilliseconds;
                    Point WolfPosition = new Point();
                    if (elapsed < MoveDurationWolf)
                    {
                        double t = elapsed / MoveDurationWolf;
                        WolfPosition = new Point(
                            Wolf.WolfPosition.X + (int)((nextStuff.X - Wolf.WolfPosition.X) * t),
                            Wolf.WolfPosition.Y + (int)((nextStuff.Y - Wolf.WolfPosition.Y) * t)
                        );
                    }
                    Wolf.WolfPosition = nextStuff;
                    wolfPositionHistory.Add(Wolf.WolfPosition);
                    Invalidate(new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize));
                    if (wolfPositionHistory.Count > maxPositionHistoryLength)
                    {
                        wolfPositionHistory.RemoveAt(0);
                    }
                    foreach (Point point in wolfPositionHistory)
                    {
                        Rectangle dirtyRect = new Rectangle(point.X, point.Y, CellSize, CellSize);
                        Invalidate(dirtyRect);
                    }
                });
                bool droped = false;


                Rectangle WolfRect = new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize);
                Rectangle RabbitRect = new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize);
                for (int i = carrots.Count - 1; i >= 0; i--)
                {
                    if (i < carrots.Count && i >= 0)
                    {
                        Rectangle CarrotPosition = new Rectangle(carrots[i].X, carrots[i].Y, CellSize, CellSize);
                        if (WolfRect.IntersectsWith(carrots[i]))
                        {
                            droped = true;
                            carrots.RemoveAt(i);
                            Thread.Sleep(2000);
                            Invalidate(CarrotPosition);
                        }
                    }
                }

                if (droped)
                {
                    if (carrots.Count == 0)
                    {
                        await Task.Delay(150);
                        WinGame();
                    }
                }
                Thread.Sleep(MoveDurationWolf);
                if (WolfRect.IntersectsWith(RabbitRect))
                {
                    Invalidate(RabbitRect);
                    deadFromWolf = true;
                    Invoke((MethodInvoker)GameOver);
                    return;
                }
            }
            /*
             * try
             {
             }
            catch(Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке Активности волка");
            }*/
        }
        bool deadFromWolf = false;
        private void GameOver()
        {
            try
            {
                if (gameIsOver) return;

                if (InvokeRequired)
                {
                    Invoke((MethodInvoker)delegate { GameOver(); });
                    return;
                }
                if (WolfFlag && deadFromWolf)
                {
                    Point oldPosition = Wolf.WolfPosition;
                    if (Wolf.WolfPosition.X % CellSize < (int)(CellSize * 0.6) || Wolf.WolfPosition.Y % CellSize < (int)(CellSize * 0.6))
                    {
                        Wolf.WolfPosition = new Point(rabbitPosition.X, rabbitPosition.Y);
                    }
                    Invalidate(new Rectangle(oldPosition.X, oldPosition.Y, CellSize, CellSize));
                    Invalidate(new Rectangle(Wolf.WolfPosition.X, Wolf.WolfPosition.Y, CellSize, CellSize));
                }
                Invalidate(new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize));

                gameIsOver = true;
                isMoving = false;
                isGameRunning = false;
                MessageBox.Show($"К сожалению, игра закончена.\nrabb X: {rabbitPosition.X} Wolf X: {Wolf.WolfPosition.X}\nrabb Y: {rabbitPosition.Y} Wolf Y: {Wolf.WolfPosition.Y}");
                Close();
            }
            catch (Exception a)
            {
                Console.WriteLine(a.ToString());
                MessageBox.Show("Вызвано исключение в блоке GameOver");
            }
        }


        private void DrawRabbit(Graphics g, int x, int y)
        {
            g.DrawImage(rabbitImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void DrawCarrot(Graphics g, int x, int y)
        {
            g.DrawImage(carrotImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void DrawWolf(Graphics g, int x, int y)
        {
            g.DrawImage(wolfImage, new Rectangle(x, y, CellSize, CellSize));
        }
    }
}

