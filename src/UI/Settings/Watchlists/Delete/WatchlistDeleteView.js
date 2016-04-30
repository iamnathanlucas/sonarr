var vent = require('vent');
var Marionette = require('marionette');

module.exports = Marionette.ItemView.extend({
	template : 'Settings/Watchlists/Delete/WatchlistDeleteViewTemplate',

	events : {
		'click .x-confirm-delete' : '_delete'
	},
	_delete : function() {
		this.model.destroy({
			wait 	: true,
			success : function() {
				ven.trigger(vent.Commands.CloseModalCommand);
			}
		});
	}
});
