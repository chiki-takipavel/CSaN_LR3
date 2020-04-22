# CSaN_LR3
## Лабораторная работа №3
Необходимо разработать программу (консольную или графическую) для обмена текстовыми сообщениями, работающую в локальной сети в **одноранговом** режиме.

Каждый участник обмена сообщениями (узел) идентифицируется **IP-адресом** и **произвольным именем**, которое задается пользователем (через параметр командной строки, конфигурационный файл или любым другим способом). *Уникальность имен не требуется*.

Каждый узел с помощью **UDP** формирует список активных узлов (IP-адреса и имена):
* после запуска узел отправляет **широковещательный пакет**, содержащий свое имя, для уведомления других узлов в сети о своем подключении к сети;
* другие узлы, получившие такой пакет, устанавливают с отправителем **TCP-соединение** для обмена сообщениями и передают по нему свое имя для идентификации в чате.

**В любой момент к чату может присоединиться новый клиент.**

Обмен сообщениями ведется с помощью **TCP** в логически общем пространстве: каждый узел поддерживает по **одному** TCP-соединению с каждым другим узлом и отправляет свои сообщения всем узлам в сети. Отключение узла должно корректно обрабатываться другими узлами.

Пользовательский интерфейс программы должен позволять вводить с клавиатуры и отправлять **сообщения**, а также просматривать **историю** событий с момента последнего запуска программы.

История должна включать следующие события в **хронологическом порядке с отметками времени**:
* входящие сообщения от других узлов (с указанием имени и IP-адреса отправителя);
* собственные отправленные сообщения;
* обнаружение нового узла;
* отключение работающего узла.

Для обмена сообщениями рекомендуется разработать свой собственный **формат сообщения**, позволяющий передавать сообщения разных типов и упрощающих передачу сообщений в потоковом режиме, используемом в TCP.

Например,

| Смещение в байтах | Содержимое                                                                                                       |
| ----------------- | ---------------------------------------------------------------------------------------------------------------- |
| 0                 | тип сообщения (1 — сообщение, 2 — передача имени, 3 — пользователь подключен, 4 — пользователь отключен и т. п.) | 
| 1                 | `n` — длина сообщения в байтах                                                                                   |
| Далее `n` байт    | содержание сообщения                                                                                             |

Для упрощения тестирования чата в многопользовательском режиме рекомендуется использовать [Docker](https://www.docker.com/ "Docker") или виртуальные машины (например, [VirtualBox](https://www.virtualbox.org/ "VirtualBox")).

### Дополнительное задание
Реализовать передачу имеющейся истории событий узлу при подключении.

После обнаружения узлов новый узел запрашивает у любого имеющегося историю событий. По TCP-соединению, установленному между этими узлами, происходит передача полной истории событий, известной узлу. После приема история должна быть отображена соответствующим образом в пользовательском интерфейсе.
