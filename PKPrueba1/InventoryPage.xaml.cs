using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;

namespace PKPrueba1;

// Pantalla que muestra el inventario del jugador en tarjetas.
public sealed partial class InventoryPage : Page
{
    // Construye la página y carga el inventario.
    public InventoryPage()
    {
        InitializeComponent();

        CargarInventario();
    }

    // Limpia la lista y crea una tarjeta por cada objeto distinto; si no hay objetos muestra un mensaje de inventario vacío.
    private void CargarInventario()
    {
        InventoryList.Items.Clear();

        Dictionary<string, int> objetos =
            InventoryManager.GetAllItems();

        if (objetos.Count == 0)
        {
            InventoryList.Items.Add(
                new TextBlock
                {
                    Text = "Tu inventario está vacío.",
                    FontSize = 22,
                    Foreground = new SolidColorBrush(Colors.White),
                    HorizontalAlignment =
                        HorizontalAlignment.Center,
                    Margin = new Thickness(20)
                });

            return;
        }

        foreach (var objeto in objetos)
        {
            Border tarjeta = CrearTarjeta(
                objeto.Key,
                objeto.Value);

            InventoryList.Items.Add(tarjeta);
        }
    }

    // Crea la tarjeta de un objeto: imagen, nombre, cantidad y descripción.
    private Border CrearTarjeta(
        string nombre,
        int cantidad)
    {
        Border tarjeta = new Border
        {
            Width = 220,
            Height = 190,
            Margin = new Thickness(10),
            Padding = new Thickness(15),
            CornerRadius = new CornerRadius(15),

            Background =
                new SolidColorBrush(
                    ColorHelper.FromArgb(
                        255,
                        25,
                        30,
                        42))
        };

        StackPanel contenido =
            new StackPanel
            {
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        Image imagen = new Image
        {
            Width = 55,
            Height = 55,
            Stretch =
                Stretch.Uniform
        };

        string sprite =
            ObtenerSprite(nombre);

        if (!string.IsNullOrWhiteSpace(sprite))
        {
            imagen.Source =
                new BitmapImage(
                    new Uri(sprite));
        }

        TextBlock nombreText =
            new TextBlock
            {
                Text = nombre,
                FontSize = 18,
                FontWeight =
                    Microsoft.UI.Text.FontWeights.Bold,
                Foreground =
                    new SolidColorBrush(Colors.White),
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(0, 5, 0, 0)
            };

        TextBlock cantidadText =
            new TextBlock
            {
                Text = $"Cantidad: {cantidad}",
                FontSize = 15,
                Foreground =
                    new SolidColorBrush(Colors.LightGray),
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

        TextBlock descripcionText =
            new TextBlock
            {
                Text = ItemCatalog.Get(nombre)?.Description ?? "",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Colors.LightSkyBlue),
                Margin = new Thickness(0, 4, 0, 0)
            };

        contenido.Children.Add(imagen);
        contenido.Children.Add(nombreText);
        contenido.Children.Add(cantidadText);
        contenido.Children.Add(descripcionText);

        tarjeta.Child = contenido;

        return tarjeta;
    }

    // Devuelve la URL del sprite del objeto (vacío si no existe en el catálogo).
    private string ObtenerSprite(string nombre)
    {
        return ItemCatalog.Get(nombre)?.SpriteUrl ?? "";
    }

    // Vuelve a la pantalla anterior o, si no hay, al menú principal.
    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(
                typeof(MainMenuPage));
    }
}