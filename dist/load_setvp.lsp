;; filepath: dist/load_setvp.lsp
;; SetVP 插件加载脚本
;; 用法：在 AutoCAD 命令行运行: (load "load_setvp")
;; 或将此文件拖入 AutoCAD 窗口

(defun C:LOAD-SETVP (/ dll-path)
  "加载 SetVP.dll 并注册 SETVP 命令"
  (setq dll-path (findfile "SetVP.dll"))
  (if (null dll-path)
    (progn
      (princ "\n[错误] 找不到 SetVP.dll，请确认文件存在于 AutoCAD 搜索路径中。")
      (princ)
    )
    (progn
      (princ (strcat "\n[SetVP] 正在加载: " dll-path))
      (command "netload" dll-path)
      (princ "\n[SetVP] 插件已加载！命令行输入: SetVP")
      (princ)
    )
  )
)

;; 拖入 CAD 时自动加载
(C:LOAD-SETVP)
(princ)
