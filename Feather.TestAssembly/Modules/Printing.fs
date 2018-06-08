module Printing

let print a = printf "Hello %s / %s" a (a.ToUpper())

let quoted b = sprintf "'%d'" b
