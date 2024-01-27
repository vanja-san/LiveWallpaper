interface API {
  GetConfig(key: string): Promise<string>;
  SetConfig(key: string, value: string);
  GetWallpapers(): Promise<string>;
  GetScreens(): Promise<string>;
  ShowWallpaper(wallpaper: string);

  /**
   * @deprecated
   */
  GetPlayingWallpaper(): Promise<string>;
  GetPlayingStatus(): Promise<string>;
  PauseWallpaper(screenIndex?: number): Promise<void>;
  ResumeWallpaper(screenIndex?: number): Promise<void>;
  StopWallpaper(screenIndex?: number): Promise<void>;
  SetVolume(volume: string, screenIndex?: string): Promise<void>;
  GetVersion(): Promise<string>;
  OpenUrl(url: string): Promise<void>;
  UploadToTmp(fileName: string, content: string): Promise<string>;
  CreateWallpaper(title: string, coverUrl: string, pathUrl: string): Promise<boolean>;
  CreateWallpaperNew(wallpaperJson: string): Promise<boolean>;
  UpdateWallpaperNew(wallpaperJson: string, oldPath: string): Promise<boolean>;
  DeleteWallpaper(wallpaperJSON: string): Promise<boolean>;
  Explore(path: string): Promise<void>;
  SetWallpaperSetting(wallpaperJSON: string, settingJSON: string): Promise<boolean>;
  addEventListener(type: string, listener: (e: any) => void);
}

interface Shell {
  ShowFolderDialog(): Promise<string>;
}

interface Window {
  chrome: {
    webview: {
      hostObjects: {
        sync: {
          api: API;
          shell: Shell;
        };
        api: API;
        shell: Shell;
      };
    };
  };
}
