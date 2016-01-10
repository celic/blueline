# == Schema Information
#
# Table name: games
#
#  id      :integer          not null, primary key
#  home_id :integer
#  away_id :integer
#  date    :date
#
# Indexes
#
#  index_games_on_away_id  (away_id)
#  index_games_on_home_id  (home_id)
#

class Game < ActiveRecord::Base

	## Relationships
	belongs_to :home, class_name: 'Team'
	belongs_to :away, class_name: 'Team'

	has_many :stats, class_name: 'GameStat'
end
