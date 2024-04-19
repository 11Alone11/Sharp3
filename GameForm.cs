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
        private Thread rabbitThread;
        private Thread carrotThread;
        private Thread trapThread;
        private readonly object syncLock = new object();


        private Point targetRabbitPosition;
        private const int moveDuration = 200; // Движение за 500 мс
        private DateTime moveStartTime;
        private Point startPosition;
        private bool isMoving = false;


        private bool isGameRunning;
        private CancellationTokenSource cancellationTokenSource;
        private Point rabbitPosition;
        private List<Rectangle> carrots;
        private List<Point> traps; // Хранит позиции ловушек

        private const int CellSize = 50;
        private const int GridWidth = 20;
        private const int GridHeight = 20;

        private Image rabbitImage;
        private Image carrotImage;
        private Image cellBackgroundImage;
        private Image trapImage;



        public GameForm()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
            this.ClientSize = new Size(CellSize * GridWidth, CellSize * GridHeight);
            cancellationTokenSource = new CancellationTokenSource();

            rabbitPosition = new Point(100, 100);

            LoadResources();
            CreateBackgroundBuffer();
            InitializeLevel();
            this.KeyDown += new KeyEventHandler(GameForm_KeyDown);
        }



        private void InitializeLevel()
        {
            isGameRunning = true;
            rabbitPosition = new Point(250, 200);

            carrots = new List<Rectangle>();

            for (int i = 0; i < GridHeight; i++)
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
            while (traps.Count < 4)
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

        private Bitmap backgroundBuffer;


        private void CreateBackgroundBuffer()
        {
            if (backgroundBuffer != null)
                backgroundBuffer.Dispose();

            backgroundBuffer = new Bitmap(CellSize * GridWidth, CellSize * GridHeight);
            using (Graphics g = Graphics.FromImage(backgroundBuffer))
            {
                for (int i = 0; i < GridWidth; i++)
                {
                    for (int j = 0; j < GridHeight; j++)
                    {
                        g.DrawImage(cellBackgroundImage, i * CellSize, j * CellSize, CellSize, CellSize);
                    }
                }
            }
        }




        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            // Отрисовка сохраненного фона из буфера
            g.DrawImage(backgroundBuffer, 0, 0);

            // Отрисовка морковок и ловушек в области, требующей перерисовки
            foreach (var carrot in carrots)
            {
                if (e.ClipRectangle.IntersectsWith(new Rectangle(carrot.X, carrot.Y, CellSize, CellSize)))
                {
                    DrawCarrot(g, carrot.X, carrot.Y);
                }
            }

            foreach (Point trap in traps)
            {
                if (e.ClipRectangle.IntersectsWith(new Rectangle(trap.X, trap.Y, CellSize, CellSize)))
                {
                    DrawTrap(g, trap.X, trap.Y);
                }
            }

            // Отрисовка кролика, если его позиция пересекается с областью перерисовки
            if (e.ClipRectangle.IntersectsWith(new Rectangle(rabbitPosition.X, rabbitPosition.Y, CellSize, CellSize)))
            {
                DrawRabbit(g, rabbitPosition.X, rabbitPosition.Y);
            }
        }



        private void DrawTrap(Graphics g, int x, int y)
        {
            g.DrawImage(trapImage, new Rectangle(x, y, CellSize, CellSize));
        }

        private void GameForm_Load(object sender, EventArgs e)
        {
            StartGame();
        }

        private async  void GameForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isGameRunning || isMoving) return; // Игнорируем ввод, если игра остановлена или идёт анимация

            var newRabbitPosition = rabbitPosition;
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

                    // Проверяем, находится ли на новой позиции ловушка
                    if (traps.Any(trap => trap.X == newRabbitPosition.X && trap.Y == newRabbitPosition.Y))
                    {
                        rabbitPosition = targetRabbitPosition; // Перемещаем кролика на клетку с ловушкой
                        Task.Delay(moveDuration).ContinueWith(t => GameOver()); // Задержка перед вызовом GameOver
                        return;
                    }
                }
            }

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
        }

        private void LoadResources()
        {
            rabbitImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Rabbit3.png");
            carrotImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Carrot4.png");
            cellBackgroundImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Grass2.png");
            trapImage = Image.FromFile("D:\\C#3\\WindowsFormsApp1\\Properties\\Trap.png");
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
                Thread.Sleep(100); // Проверка каждые 100 миллисекунд
            }
        }
        private bool gameIsOver = false;



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
                            rabbitPosition = new Point(
                                startPosition.X + (int)((targetRabbitPosition.X - startPosition.X) * t),
                                startPosition.Y + (int)((targetRabbitPosition.Y - startPosition.Y) * t)
                            );
                        }
                        else
                        {
                            rabbitPosition = targetRabbitPosition;
                            isMoving = false;

                            // Проверяем, остановился ли кролик на ловушке и заканчиваем игру
                            if (traps.Any(trap => trap.X == rabbitPosition.X && trap.Y == rabbitPosition.Y))
                            {
                                Invoke((MethodInvoker)GameOver);
                            }
                        }

                        if (!isMoving)
                        {
                            // Если кролик наступил на ловушку, останавливаем игру.
                            if (traps.Any(trap => trap.X == rabbitPosition.X && trap.Y == rabbitPosition.Y))
                            {
                                GameOver();
                            }
                        }

                        Invalidate(); // Перерисовка формы

                    });
                    Thread.Sleep(10);
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
                Thread.Sleep(100);
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

            foreach (Point trap in traps)
            {
                if (rabbitPosition == trap)
                {
                    GameOver();
                    return; // Выход, так как игра закончена
                }
            }

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

