using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    public partial class GameForm : Form
    {
        private Thread rabbitThread; // Поток кролика
        private Thread carrotThread; // Поток морковок
        private Thread trapThread;  //Поток ловушек
        private Thread wolfThread; //Поток волков
        private readonly object syncLock = new object(); //?


        private Point targetRabbitPosition; // Ключевая позиция кролика
        private const int moveDuration = 50; // Движение за 500 мс
        private DateTime moveStartTime; // Время начала движения
        private Point startPosition; // Начальная позиция кролика
        private bool isMoving = false; // Движится ли кролик


        private bool isGameRunning;  //Идет ли игра
        private CancellationTokenSource cancellationTokenSource; // ?
        private Point rabbitPosition; // Позиция кролика
        private List<Rectangle> carrots;  // Хранит позиции морковок
        private List<Point> traps; // Хранит позиции ловушек

        private const int CellSize = 50; // Размер ячейки
        private const int GridWidth = 10; // Ширина ячейки
        private const int GridHeight = 10; // Высота ячейки

        private Bitmap backgroundBuffer; // Фон глобальный

        private Image rabbitImage; // Изображение кролика
        private Image carrotImage; // Изображение морковки
        private Image cellBackgroundImage; // Фон ячейки
        private Image trapImage; // Изображение ловушек

        private bool gameIsOver = false; // Проиграна ли игра


        public GameForm()
        {
            InitializeComponent(); // Инициализация компонентов
            this.DoubleBuffered = true; // ?
            this.ClientSize = new Size(CellSize * GridWidth, CellSize * GridHeight); // Размер главной формы
            this.Text = "Собери всю морковку";
            cancellationTokenSource = new CancellationTokenSource(); //?

            rabbitPosition = new Point(100, 100);  // Позиция кролика

            LoadResources(); // Загрузка ресурсов
            CreateBackgroundBuffer(); //?
            InitializeLevel(); // Инициализация уровня
            this.KeyDown += new KeyEventHandler(GameForm_KeyDown); // Нажата кнопка в форме
        }



        private void InitializeLevel()
        {
            isGameRunning = true;    
            rabbitPosition = new Point(250, 200);// Смена позиции кролика с 100 100 в 250 200 зачем?

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


        private void CreateBackgroundBuffer()  // отрисовка фона
        {
            if (backgroundBuffer != null)
            {
                backgroundBuffer.Dispose(); // Очистка памяти занятым главным фоном
            }

            backgroundBuffer = new Bitmap(CellSize * GridWidth, CellSize * GridHeight); // Создание буфера
            using (Graphics g = Graphics.FromImage(backgroundBuffer))
            {
                for (int i = 0; i < GridWidth; i++)
                {
                    for (int j = 0; j < GridHeight; j++)
                    {
                        g.DrawImage(cellBackgroundImage, i * CellSize, j * CellSize, CellSize, CellSize); // Отрисовка ij ячейки
                    }
                }
            }
        }


        private bool isInitField = false;

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            g.DrawImage(backgroundBuffer, 0, 0);// Отрисовка сохраненного фона из буфера
            foreach (Point trap in traps)
            {
                if (e.ClipRectangle.IntersectsWith(new Rectangle(trap.X, trap.Y, CellSize, CellSize)))
                {
                    DrawTrap(g, trap.X, trap.Y);
                }
            }

            
            

            // Отрисовка морковок и ловушек в области, требующей перерисовки
            foreach (var carrot in carrots)
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
        }



        private void DrawTrap(Graphics g, int x, int y) // отрисовка ловушек
        {
            g.DrawImage(trapImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void GameForm_Load(object sender, EventArgs e)
        {
            StartGame();  // начало игры
        }

        private async  void GameForm_KeyDown(object sender, KeyEventArgs e)
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


                    /* Избыточность
                    // Проверяем, находится ли на новой позиции ловушка
                    if (traps.Any(trap => trap.X == newRabbitPosition.X && trap.Y == newRabbitPosition.Y))
                    {
                        rabbitPosition = targetRabbitPosition; // Перемещаем кролика на клетку с ловушкой
                        Task.Delay(moveDuration).ContinueWith(t => GameOver()); // Задержка перед вызовом GameOver
                        return;
                    }
                     
                     */
                }
            }
            /* FV1 + избыточность
              if (traps.Any(trap => trap.X == newRabbitPosition.X && trap.Y == newRabbitPosition.Y))
            {
                // Перемещаем кролика на клетку с ловушкой
                targetRabbitPosition = newRabbitPosition;
                isMoving = true;
                moveStartTime = DateTime.Now;
                startPosition = rabbitPosition;
            }
            else if (!isMoving && newRabbitPosition.X >= 0 && newRabbitPosition.X < CellSize * GridWidth &&
         newRabbitPosition.Y >= 0 && newRabbitPosition.Y < CellSize * GridHeight)
            {
                // Начинаем движение
                startPosition = rabbitPosition;
                targetRabbitPosition = newRabbitPosition;
                moveStartTime = DateTime.Now;
                isMoving = true;
            }
             */

        }

        private void LoadResources()
        {
            rabbitImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Rabbit.png");
            carrotImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Carrots.png");
            cellBackgroundImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Grass.png");
            trapImage = Image.FromFile("C:\\Users\\Андрей\\Downloads\\Текстуры, мне пофиг\\Spikes.png");
        }



        private void WinGame()
        {
            isGameRunning = false;
            MessageBox.Show("Вы выиграли! Все морковки собраны.");
            Close();
        }


        private void StartGame()
        {
            isGameRunning = true;

            rabbitThread = new Thread(RabbitMovement) { IsBackground = true };
            carrotThread = new Thread(CheckCarrot) { IsBackground = true };

            rabbitThread.Start();
            carrotThread.Start();

            trapThread = new Thread(TrapBehavior) { IsBackground = true };
            trapThread.Start();

        }

        private void TrapBehavior()
        {
            while (isGameRunning && !cancellationTokenSource.IsCancellationRequested)
            {
                lock (syncLock)
                {
                    foreach (Point trap in traps)
                    {
                        if (rabbitPosition == trap)
                        {
                            Invoke((MethodInvoker)GameOver);
                            return; // Завершаем поток, так как кролик пойман в ловушку
                        }
                    }
                }
                Thread.Sleep(10); // Проверка каждые 10 миллисекунд //FV2
            }
        }

        private List<Point> rabbitPositionHistory = new List<Point>();
        private const int maxPositionHistoryLength = 5;


        private void RabbitMovement()
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
                            rabbitPositionHistory.Add(targetRabbitPosition);
                            rabbitPositionHistory.Add(rabbitPosition);

                            // Ограничение длины истории
                            if (rabbitPositionHistory.Count > maxPositionHistoryLength)
                            {
                                rabbitPositionHistory.RemoveAt(0);
                            }
                            foreach (Point point in rabbitPositionHistory)
                            {
                                Rectangle dirtyRect = new Rectangle(point.X, point.Y, CellSize, CellSize);
                                Invalidate(dirtyRect);
                            }
                        }
                        else
                        {
                            rabbitPosition = targetRabbitPosition;
                            isMoving = false;
                            /*  FV1
                             if (traps.Any(trap => trap.X == rabbitPosition.X && trap.Y == rabbitPosition.Y))
                            {
                                Invoke((MethodInvoker)GameOver);
                            }
                             */
                            // Проверяем, остановился ли кролик на ловушке и заканчиваем игру

                        }
                        /*  FV1
                         
                        if (!isMoving)
                        {
                            // Если кролик наступил на ловушку, останавливаем игру.
                            if (traps.Any(trap => trap.X == rabbitPosition.X && trap.Y == rabbitPosition.Y))
                            {
                                GameOver();
                            }
                        }
                        */

                        //Invalidate(); // Перерисовка формы

                    });
                    Thread.Sleep(10); //FV2
                }
            }
        }




        private void CheckCarrot()
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


        private async void CheckRabbitCarrotCollision()
        {
            Rectangle rabbitRectangle = new Rectangle(rabbitPosition, new Size(CellSize, CellSize));
            bool eaten = false;

            for (int i = carrots.Count - 1; i >= 0; i--)
            {
                if (rabbitRectangle.IntersectsWith(carrots[i]))
                {
                    eaten = true;
                    carrots.RemoveAt(i);
                }
            }
            /* FV1 
             
            foreach (Point trap in traps)
            {
                if (rabbitPosition == trap)
                {
                    GameOver();
                    return; // Выход, так как игра закончена
                }
            }
             
            */

            if (eaten)
            {
                if (carrots.Count == 0)
                {
                    await Task.Delay(150);
                    WinGame();
                }
            }
        }

        private void GameOver()
        {
            if (gameIsOver) return;

            if (InvokeRequired)
            {
                Invoke((MethodInvoker)delegate { GameOver(); });
                return;
            }

            gameIsOver = true;
            isMoving = false;
            isGameRunning = false;
            MessageBox.Show("К сожалению, игра закончена. Вы наступили на ловушку!");
            Close();
        }


        private void DrawRabbit(Graphics g, int x, int y)
        {
            g.DrawImage(rabbitImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void DrawCarrot(Graphics g, int x, int y)
        {
            g.DrawImage(carrotImage, new Rectangle(x, y, CellSize, CellSize));
        }

    }
}

