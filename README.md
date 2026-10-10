# Reencarnación Express

Videojuego JRPG 2D desarrollado en Unity 6.

## Requisitos para trabajar en el proyecto

Antes de comenzar, cada integrante del equipo debe tener instalado:

- Unity 6 (versión 6000.6.4f1).
- Git.
- Git LFS.
- Visual Studio Code o Visual Studio.

## Configuración de Git LFS

Este proyecto utiliza **Git LFS (Large File Storage)** para administrar archivos grandes, como imágenes, sprites, música y otros recursos.

Cada integrante debe instalar Git LFS en su computadora.

**1. Instalar Git LFS**

Descargar desde:

https://git-lfs.com/

**2. Habilitar Git LFS**

Abrir PowerShell o Git Bash y ejecutar:

```bash
git lfs install
```

Este paso normalmente se realiza una sola vez por computadora.

**3. Clonar el repositorio**

```bash
git clone https://github.com/Erick-Reyes-Doc/ReencarnacinExprs.git
```

**4. Descargar los archivos LFS**

Si ya tenían clonado el repositorio antes de configurar Git LFS, ejecutar dentro de la carpeta del proyecto:

```bash
git lfs pull
```

**5. Verificar Git LFS**

```bash
git lfs ls-files
```

Este comando debe mostrar los archivos administrados mediante Git LFS.

## Importante

- No subir las carpetas `Library`, `Temp`, `Logs`, `Obj` ni `Builds`.
- No modificar ni eliminar los archivos `.meta` manualmente.
- Mantener los recursos utilizados por Unity dentro de la carpeta `Assets`.
- No modificar `.gitattributes` sin consultar con el equipo.
- No subir archivos originales de edición que no sean necesarios para ejecutar el proyecto.
- Trabajar en una rama propia para desarrollar funcionalidades.

## Organización del proyecto

- `Assets/Scenes`: escenas del videojuego.
- `Assets/Scripts`: scripts C#.
- `Assets/Sprites`: personajes, fondos y recursos gráficos.
- `Assets/Audio`: música y efectos de sonido.
- `Assets/Settings`: configuraciones de Unity.
- `Packages`: dependencias del proyecto.
- `ProjectSettings`: configuración general del proyecto.

## Solución de problemas

**¿Las imágenes no aparecen correctamente en Unity?**

Ejecutar:

```bash
git lfs pull
```

**¿Cómo comprobar que los archivos LFS están correctamente descargados?**

```bash
git lfs fsck
```

**¿Cómo revisar el tamaño de los archivos LFS?**

```bash
git lfs ls-files --size
```

---

**Proyecto académico: Reencarnación Express**
